using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Organiza.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Configure ConnectionStrings__Database.")));
builder.Services.AddIdentity<Member, IdentityRole>(o => {
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.Name = "Organiza.Session"; o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(14); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
var origin = builder.Configuration["App:PublicOrigin"] ?? "http://localhost:3000";
var allowedOrigins = builder.Environment.IsDevelopment() ? new[] {origin,"http://localhost:5080","http://127.0.0.1:3000","http://127.0.0.1:5080"} : new[] {origin};
builder.Services.Configure<IdentityPasskeyOptions>(o => {
    o.ServerDomain = builder.Configuration["App:PasskeyDomain"] ?? "localhost";
    o.UserVerificationRequirement = "required";
    o.ValidateOrigin = context => ValueTask.FromResult(!context.CrossOrigin && allowedOrigins.Contains(context.Origin));
});
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF"; o.Cookie.Name = "Organiza.Csrf"; o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddDataProtection().SetApplicationName("Organiza").PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["App:KeyPath"] ?? ".local/keys"));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddHttpClient<GoogleCalendar>();
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var network = builder.Configuration["App:ProxyNetwork"];
    if (!string.IsNullOrWhiteSpace(network)) o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});
var app = builder.Build();
if (args.Length > 0 && new[] {"init-db","bootstrap","reset-password","import"}.Contains(args[0])) {
    await Commands.Run(app.Services, args); return;
}
using (var scope = app.Services.CreateScope())
    await Schema.Initialize(scope.ServiceProvider.GetRequiredService<AppDb>());
app.UseForwardedHeaders();
app.Use(async (c, next) => {
    c.Response.Headers["X-Content-Type-Options"] = "nosniff";
    c.Response.Headers["Referrer-Policy"] = "same-origin";
    c.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (c.Request.Path.StartsWithSegments("/api")) c.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (ArgumentException ex) { await Problem(c,400,ex.Message); }
    catch (AntiforgeryValidationException) { await Problem(c,400,"A sessão do formulário expirou. Atualize a página."); }
    catch (DbUpdateConcurrencyException) { await Problem(c,409,"Este registo foi alterado por outro membro. Atualize e tente novamente."); }
    catch (DbUpdateException) { await Problem(c,409,"Não foi possível guardar. Atualize a página e tente novamente."); }
    catch (GoogleException ex) { await Problem(c,ex.Status,ex.Message); }
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.Use(async (c,next) => {
    if (c.User.Identity?.IsAuthenticated == true && c.Request.Path.StartsWithSegments("/api")) {
        var db = c.RequestServices.GetRequiredService<AppDb>();
        if (!await db.Users.AnyAsync(x => x.Id == c.User.FindFirstValue(ClaimTypes.NameIdentifier) && x.IsActive)) { c.Response.StatusCode=403; return; }
    }
    if (c.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method))
        await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
    await next();
});
app.MapGet("/api/health", async (AppDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new{status="ok"}) : Results.StatusCode(503));
app.MapGet("/api/auth/csrf", (HttpContext c, IAntiforgery a) => Results.Ok(new{token=a.GetAndStoreTokens(c).RequestToken}));
AuthEndpoints.Map(app);
var api = app.MapGroup("/api").RequireAuthorization();
EarningEndpoints.Map(api); TaskEndpoints.Map(api); MemberEndpoints.Map(api); GoogleEndpoints.Map(api);
app.MapFallback(async c => {
    if(c.Request.Path.StartsWithSegments("/api")) { c.Response.StatusCode=404; return; }
    var route = c.Request.Path.Value!.Trim('/');
    var file = Path.Combine(app.Environment.WebRootPath ?? "wwwroot",route,"index.html");
    if (!Path.HasExtension(route) && File.Exists(file)) { c.Response.ContentType="text/html"; await c.Response.SendFileAsync(file); }
    else c.Response.StatusCode=404;
});
await app.RunAsync();
static async Task Problem(HttpContext c,int status,string message) { if(c.Response.HasStarted) return; c.Response.StatusCode=status; await c.Response.WriteAsJsonAsync(new{message}); }
public partial class Program;
