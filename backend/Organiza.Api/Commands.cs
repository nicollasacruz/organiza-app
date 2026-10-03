using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Organiza.Domain;
namespace Organiza.Api;
public static class Commands
{
    public static async Task Run(IServiceProvider services,string[] args) {
        using var scope=services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var users=scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
        if(args[0]=="init-db") { await Schema.Initialize(db);Console.WriteLine("Esquema preparado; dados existentes preservados.");return; }
        if(args[0]=="bootstrap") {
            if(args.Length<3) throw new ArgumentException("bootstrap <email> <nome>");
            await Schema.Initialize(db);
            if(await db.Users.AnyAsync()) throw new InvalidOperationException("O administrador inicial já foi criado.");
            var member=new Member{UserName=args[1],Email=args[1],EmailConfirmed=true,DisplayName=args[2],IsAdmin=true};
            while(true) {
                var result=await users.CreateAsync(member,ReadPassword());
                if(result.Succeeded) break;
                if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ORGANIZA_PASSWORD")) || result.Errors.Any(e=>!e.Code.StartsWith("Password")))
                    AuthEndpoints.Check(result);
                foreach(var error in result.Errors) Console.Error.WriteLine(PasswordMessage(error));
                Console.Error.WriteLine("Tente novamente. A palavra-passe não é mostrada enquanto escreve.");
            }
            Console.WriteLine("Administrador criado.");return;
        }
        if(args[0]=="reset-password") {
            if(args.Length<2) throw new ArgumentException("reset-password <email>");var u=await users.FindByEmailAsync(args[1])??throw new ArgumentException("Membro inexistente.");
            AuthEndpoints.Check(await users.ResetPasswordAsync(u,await users.GeneratePasswordResetTokenAsync(u),ReadPassword()));Console.WriteLine("Palavra-passe atualizada.");return;
        }
        if(args[0]=="import") {
            if(args.Length<2) throw new ArgumentException("import <ficheiro.json>");
            var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};options.Converters.Add(new JsonStringEnumConverter());
            var rows=JsonSerializer.Deserialize<List<ImportedEarning>>(await File.ReadAllTextAsync(args[1]),options)??throw new ArgumentException("Importação vazia.");
            await using var tx=await db.Database.BeginTransactionAsync();var count=0;
            foreach(var row in rows) {
                var cents=Earnings.Calculate(new(row.Date,row.Kind,row.Quantity,row.Capacity));if(cents!=row.ExpectedCents || string.IsNullOrWhiteSpace(row.SourceId)) throw new ArgumentException("A linha de origem não coincide com o cálculo.");
                if(await db.Earnings.AnyAsync(x=>x.SourceId==row.SourceId)) continue;
                // A private receipt links rows already inserted through the local API.
                if(row.ExistingId is Guid existingId) {
                    var existing=await db.Earnings.FindAsync(existingId)??throw new ArgumentException("O registo do recibo de importação não existe.");
                    if(existing.SourceId is not null || existing.Date!=row.Date || existing.Kind!=row.Kind || existing.Quantity!=row.Quantity || existing.Capacity!=row.Capacity || existing.Cents!=cents)
                        throw new ArgumentException("O registo do recibo de importação foi alterado.");
                    existing.SourceId=row.SourceId;continue;
                }
                db.Earnings.Add(new Earning{Date=row.Date,Kind=row.Kind,Quantity=row.Quantity,Capacity=row.Capacity,Cents=cents,SourceId=row.SourceId});count++;
            }
            await db.SaveChangesAsync();await tx.CommitAsync();Console.WriteLine($"{count} linhas importadas.");
        }
    }
    private static string ReadPassword() {
        var env=Environment.GetEnvironmentVariable("ORGANIZA_PASSWORD");if(!string.IsNullOrEmpty(env)) return env;
        Console.Write("Palavra-passe (12+ caracteres, maiúscula, minúscula, número e símbolo): ");var chars=new List<char>();
        while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter) break;if(key.Key==ConsoleKey.Backspace){if(chars.Count>0)chars.RemoveAt(chars.Count-1);}else if(!char.IsControl(key.KeyChar))chars.Add(key.KeyChar);}
        Console.WriteLine();return new string(chars.ToArray());
    }
    private static string PasswordMessage(IdentityError error) => error.Code switch {
        "PasswordTooShort" => "Use pelo menos 12 caracteres.",
        "PasswordRequiresNonAlphanumeric" => "Inclua pelo menos um símbolo, como ! ou @.",
        "PasswordRequiresDigit" => "Inclua pelo menos um número.",
        "PasswordRequiresLower" => "Inclua pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "Inclua pelo menos uma letra maiúscula.",
        _ => error.Description
    };
}
