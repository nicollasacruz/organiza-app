using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Organiza.Api;

// Exercise the real bootstrap command on an initially empty, in-memory relational database.
// Production/development still use PostgreSQL; SQLite is only a test fixture.
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AppDb>(o => o.UseSqlite(connection));
services.AddIdentity<Member, IdentityRole>(o => {
    o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    o.Password.RequiredLength = 12;
    o.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
await using var provider = services.BuildServiceProvider();
var password = "Qa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
var previousPassword = Environment.GetEnvironmentVariable("ORGANIZA_PASSWORD");
try {
    Environment.SetEnvironmentVariable("ORGANIZA_PASSWORD", password);
    await Commands.Run(provider, ["bootstrap", "bootstrap@example.test", "Bootstrap test"]);
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
    var member = await users.FindByEmailAsync("bootstrap@example.test");
    if (member is not {IsAdmin:true, IsActive:true} || !await users.CheckPasswordAsync(member, password))
        throw new Exception("Bootstrap did not persist a usable administrator.");
    var duplicateRejected = false;
    try { await Commands.Run(provider, ["bootstrap", "other@example.test", "Other"]); }
    catch (InvalidOperationException ex) when (ex.Message == "O administrador inicial já foi criado.") { duplicateRejected = true; }
    if (!duplicateRejected || await db.Users.CountAsync() != 1)
        throw new Exception("Repeated bootstrap did not preserve the existing administrator.");
    Console.WriteLine("Bootstrap: empty schema initialized, credentials verified, repeated bootstrap rejected without data loss.");
} finally {
    Environment.SetEnvironmentVariable("ORGANIZA_PASSWORD", previousPassword);
}
