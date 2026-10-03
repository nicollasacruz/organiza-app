using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
namespace Organiza.Api;
public static class MemberEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/members",async (AppDb db) => Results.Ok(await db.Users.Where(x=>x.IsActive).OrderBy(x=>x.DisplayName).Select(x=>new{x.Id,x.DisplayName,x.IsAdmin}).ToListAsync()));
        api.MapPut("/preferences",async (PreferenceWrite input,HttpContext c,UserManager<Member> users) => {
            if(!Regex.IsMatch(input.Accent,"^#[0-9a-fA-F]{6}$") || !new[]{"light","dark","system"}.Contains(input.Theme)) throw new ArgumentException("A cor ou o modo são inválidos.");
            var u=(await users.GetUserAsync(c.User))!;u.Accent=input.Accent;u.Theme=input.Theme;AuthEndpoints.Check(await users.UpdateAsync(u));return Results.Ok(AuthEndpoints.PublicMember(u));
        });
        api.MapGet("/invitations",async (HttpContext c,AppDb db,UserManager<Member> users) => {
            if(!await Admin(c,users)) return Results.Forbid();return Results.Ok(await db.Invitations.OrderByDescending(x=>x.Expires).Select(x=>new{x.Id,x.Email,x.Expires,x.Used}).ToListAsync());
        });
        api.MapPost("/invitations",async (InviteWrite input,HttpContext c,AppDb db,UserManager<Member> users,IConfiguration config) => {
            if(!await Admin(c,users)) return Results.Forbid();
            var email=input.Email.Trim().ToLowerInvariant();if(!MailAddress.TryCreate(email,out var address) || address.Address!=email || email.Length>254) throw new ArgumentException("Email inválido.");
            if(await users.FindByEmailAsync(email) is not null) throw new ArgumentException("Já existe um membro com este email.");
            var token=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var invite=new Invitation{Email=email,TokenHash=AuthEndpoints.Hash(token),Expires=DateTimeOffset.UtcNow.AddDays(7)};
            db.Invitations.Add(invite);await db.SaveChangesAsync();return Results.Ok(new{invite.Id,invite.Expires,url=(config["App:PublicOrigin"]??"http://localhost:3000")+"/convite/?token="+token});
        });
        api.MapDelete("/invitations/{id:guid}",async (Guid id,HttpContext c,AppDb db,UserManager<Member> users) => {
            if(!await Admin(c,users)) return Results.Forbid();var i=await db.Invitations.FindAsync(id);if(i is null) return Results.NotFound();i.Used=true;i.Revision++;await db.SaveChangesAsync();return Results.NoContent();
        });
        api.MapDelete("/members/{id}",async (string id,HttpContext c,AppDb db,UserManager<Member> users) => {
            if(!await Admin(c,users)) return Results.Forbid();if(id==AuthEndpoints.UserId(c)) throw new ArgumentException("Não pode remover a sua própria conta.");
            var u=await users.FindByIdAsync(id);if(u is null || u.IsAdmin) throw new ArgumentException("Não é possível remover este administrador.");
            u.IsActive=false;AuthEndpoints.Check(await users.UpdateSecurityStampAsync(u));AuthEndpoints.Check(await users.UpdateAsync(u));
            var connection=await db.GoogleConnections.FindAsync(id);if(connection is not null) db.GoogleConnections.Remove(connection);await db.SaveChangesAsync();return Results.NoContent();
        });
    }
    private static async Task<bool> Admin(HttpContext c,UserManager<Member> users) => (await users.GetUserAsync(c.User)) is {IsAdmin:true,IsActive:true};
}
