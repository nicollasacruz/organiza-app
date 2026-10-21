using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Organiza.Api;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) { throw new Exception(message); }
    checks++;
}

var clock = new TestClock();
var store = new DeviceLoginStore(clock);
var ticket = store.Create(null)!;
Check(store.Poll(ticket.Token).Status == "expired", "O token do QR não pode recolher a sessão.");
Check(store.Poll(ticket.BrowserSecret).Status == "pending", "A TV deve aguardar aprovação.");
Check(store.Approve(ticket.Token, "member"), "Primeira aprovação válida.");
Check(!store.Approve(ticket.Token, "other"), "Outra conta não pode substituir a aprovação.");
var results = Enumerable.Range(0, 10).AsParallel().Select(_ => store.Poll(ticket.BrowserSecret)).ToArray();
Check(results.Count(x => x.MemberId == "member") == 1, "Recolha concorrente deve ser de uso único.");
Check(store.Inspect(ticket.Token) is null, "Um QR consumido não pode ser consultado.");
ticket = store.Create(null)!;
var replacement = store.Create(ticket.BrowserSecret)!;
Check(store.Inspect(ticket.Token) is null, "Um novo QR invalida o anterior da mesma TV.");
clock.Advance(TimeSpan.FromMinutes(5));
Check(!store.Approve(replacement.Token, "member"), "Não aprovar QR expirado.");
Check(store.Poll(replacement.BrowserSecret).Status == "expired", "Não recolher QR expirado.");
var bounded = new DeviceLoginStore(clock);
var accepted = Enumerable.Range(0, 500).Count(_ => bounded.Create(null) is not null);
Check(accepted == 500, "Pedidos dentro da capacidade.");
Check(bounded.Create(null) is null, "Capacidade global limitada.");
clock.Advance(TimeSpan.FromMinutes(5));
Check(bounded.Create(null) is not null, "Expirados libertam capacidade.");

// Endpoints reais com Identity/cookies e SQLite em memória, sem dados privados.
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
var serve = args.Contains("--serve");
if (serve) { builder.WebHost.UseUrls("http://127.0.0.1:5080"); }
else { builder.WebHost.UseTestServer(); }
builder.Logging.ClearProviders();
builder.Services.AddSingleton<TimeProvider>(serve ? TimeProvider.System : clock);
builder.Services.AddSingleton<DeviceLoginStore>();
builder.Services.AddDbContext<AppDb>(options => options.UseSqlite(connection));
builder.Services.AddAuthorization();
builder.Services.AddIdentity<Member, IdentityRole>(options =>
{
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("device-start", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("device-poll", limiter => { limiter.PermitLimit = 120; limiter.Window = TimeSpan.FromMinutes(1); });
});
await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    try
    {
        if (HttpMethods.IsPost(context.Request.Method))
        {
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        await next();
    }
    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; }
});
app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken }));
AuthEndpoints.Map(app);
DeviceLoginEndpoints.Map(app);
string memberId;
var password = "Qa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreatedAsync();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
    var member = new Member { Email = "device@example.test", UserName = "device@example.test", DisplayName = "Device test" };
    AuthEndpoints.Check(await users.CreateAsync(member, password));
    memberId = member.Id;
}
if (serve)
{
    Directory.CreateDirectory(".local");
    await File.WriteAllTextAsync(".local/device-qa.json", JsonSerializer.Serialize(new { email = "device@example.test", password }));
    if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(".local/device-qa.json", UnixFileMode.UserRead | UnixFileMode.UserWrite); }
    Console.WriteLine("API de teste em 127.0.0.1:5080, com SQLite efémero e conta fictícia em .local/device-qa.json.");
    await app.RunAsync();
    return;
}
await app.StartAsync();
var tv = new Browser(app.GetTestClient());
var phone = new Browser(app.GetTestClient());
var stranger = new Browser(app.GetTestClient());
using (var response = await tv.Post("start", csrf: false)) { Check(response.StatusCode == HttpStatusCode.BadRequest, "Criar exige CSRF."); }
using (var response = await phone.Send("/api/auth/login", new { email = "device@example.test", password }))
{
    Check(response.IsSuccessStatusCode, "Login normal do telemóvel.");
}
using (var response = await tv.Post("start"))
{
    Check(response.IsSuccessStatusCode, "TV cria QR.");
    Check(response.Headers.GetValues("Set-Cookie").Any(x => x.Contains("Organiza.Device=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase) && x.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)), "Segredo em cookie HttpOnly e Strict.");
    ticket = new DeviceLoginStore.Ticket((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!, "", "", default);
}
using (var response = await stranger.Post("approve", new { token = ticket.Token })) { Check(response.StatusCode == HttpStatusCode.Unauthorized, "Aprovação exige login."); }
using (var response = await phone.Post("inspect", new { token = ticket.Token })) { Check(response.IsSuccessStatusCode, "Telemóvel consulta código para comparar."); }
using (var response = await phone.Post("approve", new { token = ticket.Token }, csrf: false)) { Check(response.StatusCode == HttpStatusCode.BadRequest, "Aprovação exige CSRF."); }
using (var response = await phone.Post("approve", new { token = ticket.Token })) { Check(response.IsSuccessStatusCode, "Aprovação autenticada."); }
using (var response = await phone.Post("approve", new { token = ticket.Token })) { Check(response.StatusCode == HttpStatusCode.BadRequest, "Não repetir aprovação."); }
using (var response = await stranger.Post("poll", new { token = ticket.Token })) { Check(await Status(response) == "expired", "Outro navegador com o QR não recebe sessão."); }
using (var response = await tv.Post("poll")) { Check(await Status(response) == "approved", "TV original recebe sessão."); }
using (var response = await tv.Get("/api/auth/session")) { Check(response.IsSuccessStatusCode, "Cookie Identity válido na TV."); }
using (var response = await tv.Post("poll")) { Check(await Status(response) == "expired", "Não repetir recolha."); }

var expiryTv = new Browser(app.GetTestClient());
var expiryToken = await Start(expiryTv);
clock.Advance(TimeSpan.FromMinutes(5));
using (var response = await phone.Post("approve", new { token = expiryToken })) { Check(response.StatusCode == HttpStatusCode.BadRequest, "Expiração aplicada no endpoint."); }
using (var response = await expiryTv.Post("poll")) { Check(await Status(response) == "expired", "TV recebe expiração."); }

foreach (var locked in new[] { false, true })
{
    var blockedTv = new Browser(app.GetTestClient());
    var token = await Start(blockedTv);
    using (var response = await phone.Post("approve", new { token })) { Check(response.IsSuccessStatusCode, "Aprovar antes de revogar."); }
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
        var member = (await users.FindByIdAsync(memberId))!;
        member.IsActive = locked;
        member.LockoutEnd = locked ? DateTimeOffset.UtcNow.AddHours(1) : null;
        await users.UpdateAsync(member);
    }
    using (var response = await blockedTv.Post("poll")) { Check(await Status(response) == "expired", "Revalidar conta desativada/bloqueada na recolha."); }
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
        var member = (await users.FindByIdAsync(memberId))!;
        member.IsActive = true; member.LockoutEnd = null;
        await users.UpdateAsync(member);
    }
}
var finalTv = new Browser(app.GetTestClient());
using (var response = await finalTv.Post("start")) { Check(response.StatusCode == HttpStatusCode.TooManyRequests, "Limite de criação de QR aplicado."); }
Console.WriteLine($"Device login: {checks} verificações aprovadas (store e endpoints HTTP com Identity/CSRF).");

static async Task<string> Status(HttpResponseMessage response)
{
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()!;
}
static async Task<string> Start(Browser browser)
{
    using var response = await browser.Post("start");
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan interval) => now += interval;
}

sealed class Browser(HttpClient client)
{
    private readonly CookieContainer cookies = new();
    public Task<HttpResponseMessage> Post(string action, object? body = null, bool csrf = true) => Send("/api/auth/device/" + action, body, csrf);
    public async Task<HttpResponseMessage> Send(string path, object? body = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (csrf)
        {
            using var response = await Get("/api/auth/csrf");
            var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            request.Headers.Add("X-CSRF", token);
        }
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await Execute(request);
    }
    public async Task<HttpResponseMessage> Get(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await Execute(request);
    }
    private async Task<HttpResponseMessage> Execute(HttpRequestMessage request)
    {
        var uri = new Uri(client.BaseAddress!, request.RequestUri!);
        var cookie = cookies.GetCookieHeader(uri);
        if (cookie.Length > 0) { request.Headers.Add("Cookie", cookie); }
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values) { cookies.SetCookies(uri, value); }
        }
        return response;
    }
}
