using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
namespace Organiza.Api;

public static class AuthEndpoints
{
    public static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static object PublicMember(Member u) => new { u.Id, u.DisplayName, u.Email, u.IsAdmin, u.Accent, u.Theme };
    public static void Check(IdentityResult result) { if(!result.Succeeded) throw new ArgumentException(string.Join(" ",result.Errors.Select(x=>x.Description))); }
    public static void Map(WebApplication app)
    {
        var auth=app.MapGroup("/api/auth");
        auth.MapGet("/session",async (HttpContext c,UserManager<Member> users) => {
            var u=await users.GetUserAsync(c.User); return u is {IsActive:true} ? Results.Ok(PublicMember(u)) : Results.Unauthorized();
        });
        auth.MapPost("/login",async (LoginInput input,UserManager<Member> users,SignInManager<Member> signIn) => {
            var u=await users.FindByEmailAsync(input.Email.Trim());
            if(u is not {IsActive:true}) return Results.Json(new{message="Email ou palavra-passe incorretos."},statusCode:401);
            var result=await signIn.PasswordSignInAsync(u,input.Password,true,true);
            return result.Succeeded ? Results.Ok(PublicMember(u)) : Results.Json(new{message=result.IsLockedOut ? "Demasiadas tentativas. Tente novamente mais tarde." : "Email ou palavra-passe incorretos."},statusCode:401);
        }).RequireRateLimiting("auth");
        auth.MapPost("/logout",async (SignInManager<Member> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        auth.MapGet("/invitation",async (string token,AppDb db) => {
            var hash=Hash(token);var i=await db.Invitations.SingleOrDefaultAsync(x=>x.TokenHash==hash && !x.Used && x.Expires>DateTimeOffset.UtcNow);
            return i is null ? Results.NotFound(new{message="O convite expirou ou já foi utilizado."}) : Results.Ok(new{i.Email,i.Expires});
        }).RequireRateLimiting("auth");
        auth.MapPost("/signup",async (SignupInput input,AppDb db,UserManager<Member> users,SignInManager<Member> signIn) => {
            await using var tx=await db.Database.BeginTransactionAsync();
            var hash=Hash(input.Token);
            var i=await db.Invitations.FromSqlInterpolated($"SELECT * FROM \"Invitations\" WHERE \"TokenHash\"={hash} FOR UPDATE").SingleOrDefaultAsync();
            if(i is null || i.Used || i.Expires<=DateTimeOffset.UtcNow) return Results.BadRequest(new{message="O convite expirou ou já foi utilizado."});
            var name=input.Name.Trim();if(name.Length is < 1 or > 80) throw new ArgumentException("Indique um nome até 80 caracteres.");
            var u=new Member { Email=i.Email,UserName=i.Email,DisplayName=name,EmailConfirmed=true };
            Check(await users.CreateAsync(u,input.Password));i.Used=true;i.Revision++;await db.SaveChangesAsync();await tx.CommitAsync();await signIn.SignInAsync(u,true);
            return Results.Ok(PublicMember(u));
        }).RequireRateLimiting("auth");
        auth.MapPost("/passkey/options",async (SignInManager<Member> s) => Results.Content(await s.MakePasskeyRequestOptionsAsync(null),"application/json")).RequireRateLimiting("auth");
        auth.MapPost("/passkey/login",async (CredentialInput input,SignInManager<Member> s,UserManager<Member> users) => {
            var result=await s.PerformPasskeyAssertionAsync(input.CredentialJson);
            if(!result.Succeeded || result.User is not {IsActive:true}) return Results.Unauthorized();
            if(await users.IsLockedOutAsync(result.User)) return Results.Unauthorized();
            Check(await users.AddOrUpdatePasskeyAsync(result.User,result.Passkey));
            await s.SignInAsync(result.User,true);return Results.Ok(PublicMember(result.User));
        }).RequireRateLimiting("auth");
        var keys=auth.MapGroup("/passkeys").RequireAuthorization();
        keys.MapGet("/",async (HttpContext c,UserManager<Member> users) => Results.Ok((await users.GetPasskeysAsync((await users.GetUserAsync(c.User))!)).Select(k=>new{id=WebEncoders.Base64UrlEncode(k.CredentialId),k.Name,k.CreatedAt})));
        keys.MapPost("/options",async (HttpContext c,UserManager<Member> users,SignInManager<Member> s) => {
            var u=(await users.GetUserAsync(c.User))!;
            return Results.Content(await s.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity{Id=u.Id,Name=u.Email!,DisplayName=u.DisplayName}),"application/json");
        });
        keys.MapPost("/",async (CredentialInput input,HttpContext c,UserManager<Member> users,SignInManager<Member> s) => {
            var result=await s.PerformPasskeyAttestationAsync(input.CredentialJson);
            if(!result.Succeeded || result.UserEntity.Id!=UserId(c)) return Results.BadRequest(new{message="Não foi possível validar a passkey."});
            result.Passkey.Name=string.IsNullOrWhiteSpace(input.Name) ? "O meu dispositivo" : input.Name.Trim()[..Math.Min(input.Name.Trim().Length,80)];
            Check(await users.AddOrUpdatePasskeyAsync((await users.GetUserAsync(c.User))!,result.Passkey));return Results.NoContent();
        });
        keys.MapDelete("/{id}",async (string id,HttpContext c,UserManager<Member> users) => {
            Check(await users.RemovePasskeyAsync((await users.GetUserAsync(c.User))!,WebEncoders.Base64UrlDecode(id)));return Results.NoContent();
        });
    }
}
