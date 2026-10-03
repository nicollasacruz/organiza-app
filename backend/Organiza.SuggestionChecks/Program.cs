using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Organiza.Api;
using Organiza.Domain;

var checks = 0;
void Check(bool value) { if (!value) throw new Exception("Verificação de integração falhou."); checks++; }
var now = new DateTimeOffset(2026,10,3,18,30,0,TimeSpan.Zero);
var member = new SuggestionMember("member-test","Participante de teste");
var task = new SuggestionTask(Guid.NewGuid(),"Organizar uma gaveta de teste",member.Id,Planning.Today(now));
var state = new SuggestionState([new(member.Id,Planning.Today(now),new(20,0),new(21,0))],[new(task.Id,15)],[]);
var expected = new AiSuggestion("Confirme os horários e a duração estimada.",true,state);
var payload = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(expected,new JsonSerializerOptions(JsonSerializerDefaults.Web)) } } } });
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OpenRouter:ApiKey"]="synthetic-test-token" }).Build();
var handler = new FixtureHandler(payload,HttpStatusCode.OK);
var client = new OpenRouterSuggestions(new HttpClient(handler),configuration);
var messages = new[] {new SuggestionMessage("user","Tenho tempo hoje das 20h às 21h.",now)};
var reply = await client.Chat(messages,SuggestionState.Empty,[member],[task],now,CancellationToken.None);
Check(reply.Ready);Check(reply.State.Estimates.Single().Minutes==15);
using(var request = JsonDocument.Parse(handler.RequestBody!))
{
    Check(request.RootElement.GetProperty("model").GetString()=="openrouter/free");
    Check(request.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
    Check(request.RootElement.GetProperty("response_format").GetProperty("type").GetString()=="json_schema");
    Check(!handler.RequestBody!.Contains("synthetic-test-token"));
    Check(handler.RequestBody!.Contains("Europe/Lisbon"));
    Check(handler.RequestBody!.Contains("Momento da mensagem"));
    Check(handler.RequestBody!.Contains(task.Title));
}
foreach(var (body,status) in new[]{("{}",HttpStatusCode.OK),("invalid",HttpStatusCode.OK),("{}",HttpStatusCode.TooManyRequests),("{}",HttpStatusCode.ServiceUnavailable)})
{
    var failing = new OpenRouterSuggestions(new HttpClient(new FixtureHandler(body,status)),configuration);
    try { await failing.Chat(messages,state,[member],[task],now,CancellationToken.None);throw new Exception("Falha do fornecedor não foi detetada."); }
    catch(Exception ex) when(ex is HttpRequestException or JsonException) { checks++; }
}
var absent = new OpenRouterSuggestions(new HttpClient(handler),new ConfigurationBuilder().Build());
Check(!absent.Configured);
try { await absent.Chat(messages,state,[member],[task],now,CancellationToken.None);throw new Exception("Chave ausente aceite."); }
catch(InvalidOperationException) { checks++; }
Console.WriteLine($"{checks} verificações de integração OpenRouter aprovadas (fornecedor simulado, sem chamadas externas).");

sealed class FixtureHandler(string response,HttpStatusCode status) : HttpMessageHandler
{
    public string? RequestBody { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(response,System.Text.Encoding.UTF8,"application/json") };
    }
}
