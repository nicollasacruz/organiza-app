using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Organiza.Api;

// Pedidos efémeros: reiniciar a API invalida os QR pendentes, sem afetar sessões.
public sealed class DeviceLoginStore(TimeProvider clock)
{
    private sealed record Request(string BrowserHash, string Code, DateTimeOffset Expires)
    {
        public string? MemberId { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Request> requests = new();
    public record Ticket(string Token, string BrowserSecret, string Code, DateTimeOffset Expires);
    public record Details(string Code, DateTimeOffset Expires);

    public Ticket? Create(string? previousSecret)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            var previousHash = AuthEndpoints.Hash(previousSecret ?? "");
            foreach (var entry in requests.Where(x => x.Value.Expires <= now || x.Value.BrowserHash == previousHash).ToArray())
            {
                requests.Remove(entry.Key);
            }
            if (requests.Count >= 500) { return null; }
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var browserSecret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expires = now.AddMinutes(5);
            requests.Add(AuthEndpoints.Hash(token), new Request(AuthEndpoints.Hash(browserSecret), code, expires));
            return new Ticket(token, browserSecret, code, expires);
        }
    }

    public Details? Inspect(string token)
    {
        lock (gate)
        {
            var request = Find(token);
            if (request is null || request.MemberId is not null) { return null; }
            return new Details(request.Code, request.Expires);
        }
    }

    public bool Approve(string token, string memberId)
    {
        lock (gate)
        {
            var request = Find(token);
            if (request is null || request.MemberId is not null) { return false; }
            request.MemberId = memberId;
            return true;
        }
    }

    public (string Status, string? MemberId) Poll(string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length != 43) { return ("expired", null); }
        lock (gate)
        {
            var hash = AuthEndpoints.Hash(secret);
            var entry = requests.FirstOrDefault(x => x.Value.BrowserHash == hash);
            if (entry.Value is null) { return ("expired", null); }
            if (entry.Value.Expires <= clock.GetUtcNow())
            {
                requests.Remove(entry.Key);
                return ("expired", null);
            }
            if (entry.Value.MemberId is null) { return ("pending", null); }
            // Consumir sob o mesmo bloqueio impede duas sessões com o mesmo pedido.
            requests.Remove(entry.Key);
            return ("approved", entry.Value.MemberId);
        }
    }

    private Request? Find(string token)
    {
        if (token.Length != 43) { return null; }
        var hash = AuthEndpoints.Hash(token);
        if (!requests.TryGetValue(hash, out var request)) { return null; }
        if (request.Expires <= clock.GetUtcNow())
        {
            requests.Remove(hash);
            return null;
        }
        return request;
    }
}

public record DeviceApprovalInput(string Token);

public static class DeviceLoginEndpoints
{
    private const string Cookie = "Organiza.Device";
    private const string CookiePath = "/api/auth/device";

    public static void Map(WebApplication app)
    {
        var group = app.MapGroup(CookiePath);
        group.MapPost("/start", (HttpContext context, DeviceLoginStore store, IWebHostEnvironment environment) =>
        {
            var ticket = store.Create(context.Request.Cookies[Cookie]);
            if (ticket is null) { return Results.StatusCode(429); }
            context.Response.Cookies.Append(Cookie, ticket.BrowserSecret, new CookieOptions
            {
                HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
                Path = CookiePath, Expires = ticket.Expires, IsEssential = true
            });
            return Results.Ok(new { ticket.Token, ticket.Code, ticket.Expires });
        }).RequireRateLimiting("device-start");

        group.MapPost("/inspect", (DeviceApprovalInput input, DeviceLoginStore store) =>
        {
            var details = store.Inspect(input.Token ?? "");
            return details is null ? InvalidCode() : Results.Ok(details);
        }).RequireAuthorization().RequireRateLimiting("auth");

        group.MapPost("/approve", async (DeviceApprovalInput input, HttpContext context, DeviceLoginStore store, UserManager<Member> users) =>
        {
            var member = await users.GetUserAsync(context.User);
            if (member is not { IsActive: true } || await users.IsLockedOutAsync(member)) { return Results.Unauthorized(); }
            return store.Approve(input.Token ?? "", member.Id) ? Results.NoContent() : InvalidCode();
        }).RequireAuthorization().RequireRateLimiting("auth");

        group.MapPost("/poll", async (HttpContext context, DeviceLoginStore store, UserManager<Member> users, SignInManager<Member> signIn) =>
        {
            var result = store.Poll(context.Request.Cookies[Cookie]);
            if (result.Status == "pending") { return Results.Ok(new { status = "pending" }); }
            context.Response.Cookies.Delete(Cookie, new CookieOptions { Path = CookiePath });
            if (result.MemberId is null) { return Results.Ok(new { status = "expired" }); }
            var member = await users.FindByIdAsync(result.MemberId);
            if (member is not { IsActive: true } || await users.IsLockedOutAsync(member))
            {
                return Results.Ok(new { status = "expired" });
            }
            await signIn.SignInAsync(member, isPersistent: true);
            return Results.Ok(new { status = "approved" });
        }).RequireRateLimiting("device-poll");
    }

    private static IResult InvalidCode() => Results.BadRequest(new { message = "Este QR code expirou ou já foi utilizado. Gere um novo na TV." });
}
