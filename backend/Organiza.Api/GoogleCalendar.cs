using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
namespace Organiza.Api;
public class GoogleException(int status,string message):Exception(message) { public int Status=>status; }
public record GoogleTokens(string AccessToken,string RefreshToken,DateTimeOffset Expires);
public record OAuthState(string MemberId,string Verifier,string Nonce,DateTimeOffset Expires);
public class GoogleCalendar(HttpClient http,IConfiguration config,IDataProtectionProvider protection,AppDb db)
{
    private readonly IDataProtector tokens=protection.CreateProtector("Organiza.Google.Tokens.v1");
    private readonly IDataProtector state=protection.CreateProtector("Organiza.Google.OAuth.v1");
    public bool Configured => !string.IsNullOrEmpty(config["Google:ClientId"]) && !string.IsNullOrEmpty(config["Google:ClientSecret"]) && !string.IsNullOrEmpty(config["Google:RedirectUri"]);
    public async Task<object> Status(string memberId) => new{configured=Configured,connected=await db.GoogleConnections.AnyAsync(x=>x.MemberId==memberId)};
    public string Start(HttpContext c) {
        if(!Configured) throw new GoogleException(503,"Configure a integração Google no servidor.");
        var verifier=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));var nonce=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        c.Response.Cookies.Append("Organiza.GoogleState",nonce,new CookieOptions{HttpOnly=true,SameSite=SameSiteMode.Lax,Secure=c.Request.IsHttps,MaxAge=TimeSpan.FromMinutes(10),Path="/api/integrations/google"});
        var encoded=state.Protect(JsonSerializer.Serialize(new OAuthState(AuthEndpoints.UserId(c),verifier,nonce,DateTimeOffset.UtcNow.AddMinutes(10))));
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth",new Dictionary<string,string?>{
            ["client_id"]=config["Google:ClientId"],["redirect_uri"]=config["Google:RedirectUri"],["response_type"]="code",
            ["scope"]="https://www.googleapis.com/auth/calendar.calendarlist.readonly https://www.googleapis.com/auth/calendar.events",
            ["access_type"]="offline",["prompt"]="consent",["state"]=encoded,["code_challenge_method"]="S256",["code_challenge"]=WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        });
    }
    public async Task Callback(HttpContext c,string encoded,string code) {
        OAuthState payload;
        try {payload=JsonSerializer.Deserialize<OAuthState>(state.Unprotect(encoded))!;} catch(CryptographicException){throw new GoogleException(400,"A autorização Google expirou.");}
        if(payload is null || payload.MemberId!=AuthEndpoints.UserId(c) || payload.Expires<DateTimeOffset.UtcNow || c.Request.Cookies["Organiza.GoogleState"]!=payload.Nonce) throw new GoogleException(400,"A autorização Google expirou.");
        c.Response.Cookies.Delete("Organiza.GoogleState",new CookieOptions{Path="/api/integrations/google"});
        var result=await Token(new(){["grant_type"]="authorization_code",["code"]=code,["redirect_uri"]=config["Google:RedirectUri"]!,["code_verifier"]=payload.Verifier});
        var access=result.GetProperty("access_token").GetString()!;
        if(!result.TryGetProperty("refresh_token",out var refresh)) throw new GoogleException(400,"Autorize novamente o acesso à agenda.");
        using var calendars=await Send(access,HttpMethod.Get,"https://www.googleapis.com/calendar/v3/users/me/calendarList");
        var account=calendars.RootElement.GetProperty("items").EnumerateArray().FirstOrDefault(x=>x.TryGetProperty("primary",out var primary) && primary.GetBoolean());
        if(account.ValueKind==JsonValueKind.Undefined) throw new GoogleException(400,"Não foi encontrado o calendário principal desta conta.");
        var connection=await db.GoogleConnections.FindAsync(payload.MemberId);
        if(connection is null){connection=new GoogleConnection{MemberId=payload.MemberId};db.GoogleConnections.Add(connection);}
        connection.AccountId=account.GetProperty("id").GetString()!;
        connection.ProtectedTokens=tokens.Protect(JsonSerializer.Serialize(new GoogleTokens(access,refresh.GetString()!,DateTimeOffset.UtcNow.AddSeconds(result.GetProperty("expires_in").GetInt32()))));
        await db.SaveChangesAsync();
    }
    public async Task<object> Calendars(string memberId) {
        var (access,_)=await Access(memberId);
        using var result=await Send(access,HttpMethod.Get,"https://www.googleapis.com/calendar/v3/users/me/calendarList?minAccessRole=writer&maxResults=250");
        return result.RootElement.GetProperty("items").EnumerateArray().Where(x=>new[]{"owner","writer"}.Contains(x.GetProperty("accessRole").GetString())).Select(x=>new{id=x.GetProperty("id").GetString(),name=x.GetProperty("summary").GetString()}).ToArray();
    }
    public async Task<object> Copy(string memberId,CalendarWrite input) {
        var (access,connection)=await Access(memberId);
        await using var tx=await db.Database.BeginTransactionAsync();
        var task=await db.TaskItems.FromSqlInterpolated($"SELECT * FROM \"TaskItems\" WHERE \"Id\"={input.TaskId} FOR UPDATE").SingleOrDefaultAsync();
        if(task is null || task.Archived) throw new ArgumentException("Escolha uma ocorrência real e ativa.");
        var saved=await db.CalendarCopies.SingleOrDefaultAsync(x=>x.TaskId==input.TaskId && x.MemberId==memberId && x.AccountId==connection.AccountId && x.CalendarId==input.CalendarId);
        if(saved is not null) return new{saved.Url,saved.EventId,alreadySaved=true};
        if(input.CalendarId.Length is <1 or >512) throw new ArgumentException("Calendário inválido.");
        using var calendar=await Send(access,HttpMethod.Get,"https://www.googleapis.com/calendar/v3/users/me/calendarList/"+Uri.EscapeDataString(input.CalendarId));
        if(!new[]{"writer","owner"}.Contains(calendar.RootElement.GetProperty("accessRole").GetString())) throw new GoogleException(403,"Não tem acesso de escrita neste calendário.");
        var id=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{input.TaskId}|{memberId}|{connection.AccountId}|{input.CalendarId}")));
        var body=new Dictionary<string,object>{["id"]=id,["summary"]=task.Title,["description"]=task.Description+"\n\n"+(config["App:PublicOrigin"]??"http://localhost:3000")+"/tarefas/?task="+task.Id,["extendedProperties"]=new{ @private=new{organizaTask=task.Id.ToString(),organizaMember=memberId}},["reminders"]=new{useDefault=true}};
        if(input.AllDay) {body["start"]=new{date=input.Date.ToString("yyyy-MM-dd")};body["end"]=new{date=input.Date.AddDays(1).ToString("yyyy-MM-dd")};}
        else {
            if(!TimeOnly.TryParseExact(input.Time,"HH:mm",out var time) || input.DurationMinutes is <1 or >1440) throw new ArgumentException("Indique hora e duração de 1 a 1440 minutos.");
            var start=input.Date.ToDateTime(time);var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
            if(zone.IsInvalidTime(start)) throw new ArgumentException("Esta hora não existe devido à mudança da hora em Lisboa.");
            var offset=new DateTimeOffset(start,zone.GetUtcOffset(start));
            body["start"]=new{dateTime=offset.ToString("o"),timeZone="Europe/Lisbon"};body["end"]=new{dateTime=offset.AddMinutes(input.DurationMinutes).ToString("o"),timeZone="Europe/Lisbon"};
        }
        var url="https://www.googleapis.com/calendar/v3/calendars/"+Uri.EscapeDataString(input.CalendarId)+"/events";
        using var request=new HttpRequestMessage(HttpMethod.Post,url){Content=JsonContent.Create(body)};request.Headers.Authorization=new("Bearer",access);
        using var response=await http.SendAsync(request);JsonDocument result;
        if(response.StatusCode==HttpStatusCode.Conflict) {
            result=await Send(access,HttpMethod.Get,url+"/"+id);
            if(!result.RootElement.TryGetProperty("extendedProperties",out var props) || props.GetProperty("private").GetProperty("organizaTask").GetString()!=task.Id.ToString()) { result.Dispose();throw new GoogleException(409,"O evento existente não coincide com esta tarefa."); }
        } else {await Ensure(response);result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());}
        using(result){saved=new CalendarCopy{TaskId=task.Id,MemberId=memberId,AccountId=connection.AccountId,CalendarId=input.CalendarId,EventId=id,Url=result.RootElement.GetProperty("htmlLink").GetString()!};}
        db.CalendarCopies.Add(saved);await db.SaveChangesAsync();await tx.CommitAsync();return new{saved.Url,saved.EventId,alreadySaved=false};
    }
    public async Task Disconnect(string memberId) {
        var connection=await db.GoogleConnections.FindAsync(memberId);if(connection is null)return;
        // Removing tokens disconnects this application; independent calendar copies stay in Google.
        db.GoogleConnections.Remove(connection);await db.SaveChangesAsync();
    }
    private async Task<(string,GoogleConnection)> Access(string memberId) {
        var connection=await db.GoogleConnections.FindAsync(memberId)??throw new GoogleException(409,"Ligue a sua conta Google nas configurações.");
        var t=JsonSerializer.Deserialize<GoogleTokens>(tokens.Unprotect(connection.ProtectedTokens))!;
        if(t.Expires>DateTimeOffset.UtcNow.AddMinutes(1)) return(t.AccessToken,connection);
        JsonElement result;
        try {result=await Token(new(){["grant_type"]="refresh_token",["refresh_token"]=t.RefreshToken});}
        catch(GoogleException ex) when(ex.Status==401) {db.GoogleConnections.Remove(connection);await db.SaveChangesAsync();throw new GoogleException(401,"A ligação Google expirou. Volte a ligá-la.");}
        t=new(result.GetProperty("access_token").GetString()!,result.TryGetProperty("refresh_token",out var refresh)?refresh.GetString()!:t.RefreshToken,DateTimeOffset.UtcNow.AddSeconds(result.GetProperty("expires_in").GetInt32()));
        connection.ProtectedTokens=tokens.Protect(JsonSerializer.Serialize(t));await db.SaveChangesAsync();return(t.AccessToken,connection);
    }
    private async Task<JsonElement> Token(Dictionary<string,string> body) {
        body["client_id"]=config["Google:ClientId"]!;body["client_secret"]=config["Google:ClientSecret"]!;
        using var response=await http.PostAsync("https://oauth2.googleapis.com/token",new FormUrlEncodedContent(body));
        if(!response.IsSuccessStatusCode) throw new GoogleException(response.StatusCode==HttpStatusCode.BadRequest?401:502,"Não foi possível autorizar o Google. Volte a ligar a conta.");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return json.RootElement.Clone();
    }
    private async Task<JsonDocument> Send(string access,HttpMethod method,string url) {
        using var request=new HttpRequestMessage(method,url);request.Headers.Authorization=new("Bearer",access);
        using var response=await http.SendAsync(request);await Ensure(response);return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
    private static Task Ensure(HttpResponseMessage response) {
        if(!response.IsSuccessStatusCode) throw new GoogleException(response.StatusCode==HttpStatusCode.Unauthorized?409:502,"A Google Agenda não respondeu. Verifique a ligação e tente novamente.");return Task.CompletedTask;
    }
}
public static class GoogleEndpoints {
    public static void Map(RouteGroupBuilder api) {
        var group=api.MapGroup("/integrations/google");
        group.MapGet("/status",async(HttpContext c,GoogleCalendar google)=>Results.Ok(await google.Status(AuthEndpoints.UserId(c))));
        group.MapPost("/connect",(HttpContext c,GoogleCalendar google)=>Results.Ok(new{url=google.Start(c)}));
        group.MapGet("/callback",async(HttpContext c,GoogleCalendar google,IConfiguration config)=>{
            var target=(config["App:PublicOrigin"]??"http://localhost:3000")+"/configuracoes/?google=";
            if(c.Request.Query.ContainsKey("error")) return Results.Redirect(target+"cancelled");
            try{await google.Callback(c,c.Request.Query["state"].ToString(),c.Request.Query["code"].ToString());return Results.Redirect(target+"connected");}
            catch(GoogleException){return Results.Redirect(target+"failed");}
        });
        group.MapGet("/calendars",async(HttpContext c,GoogleCalendar google)=>Results.Ok(await google.Calendars(AuthEndpoints.UserId(c))));
        group.MapGet("/copies",async(HttpContext c,AppDb db)=>Results.Ok(await db.CalendarCopies.Where(x=>x.MemberId==AuthEndpoints.UserId(c)).Select(x=>new{x.TaskId,x.CalendarId,x.Url}).ToListAsync()));
        group.MapPost("/events",async(CalendarWrite input,HttpContext c,GoogleCalendar google)=>Results.Ok(await google.Copy(AuthEndpoints.UserId(c),input)));
        group.MapDelete("/connection",async(HttpContext c,GoogleCalendar google)=>{await google.Disconnect(AuthEndpoints.UserId(c));return Results.NoContent();});
    }
}
