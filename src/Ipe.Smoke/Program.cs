// console smoke test of the agent, Local mode (data/csv + data/docs), remote model.
// Usage: AzureOpenAI__Endpoint=https://... AzureOpenAI__Deployment=gpt-5-mini dotnet run --project src/Ipe.Smoke
using System.Diagnostics;
using Ipe.Agent;
using Ipe.Agent.Data;
using Ipe.Agent.Knowledge;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var cfg = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var data = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));
var repository = new LocalOperationsRepository(Path.Combine(data, "csv"));
var sp = new ServiceCollection()
    .AddLogging()
    .AddSingleton<IConfiguration>(cfg)
    .AddSingleton<IOperationsRepository>(repository)
    .AddSingleton<IKnowledgeBase>(new LocalKnowledgeBase(Path.Combine(data, "docs")))
    .BuildServiceProvider();

var agent = AnalystAgent.Create(AnalystAgent.ChatClient(cfg), sp);
var session = await agent.CreateSessionAsync();
var failures = 0;
var sessionTools = new List<string>();

void Check(bool ok, string description)
{
    Console.WriteLine($"  {(ok ? "✅" : "❌")} {description}");
    if (!ok) failures++;
}

async Task<(string Text, List<string> Tools, List<ToolApprovalRequestContent> Requests)> Ask(IEnumerable<ChatMessage> messages)
{
    var clock = Stopwatch.StartNew();
    var response = await agent.RunAsync(messages, session);
    var contents = response.Messages.SelectMany(m => m.Contents).ToList();
    var tools = contents.OfType<FunctionCallContent>().Select(c => c.Name).ToList();
    var requests = contents.OfType<ToolApprovalRequestContent>().ToList();
    sessionTools.AddRange(tools);
    Console.WriteLine($"  tools: {string.Join(", ", tools)}");
    Console.WriteLine($"  tempo: {clock.Elapsed.TotalSeconds:F0}s");
    Console.WriteLine(Indent(response.Text));
    return (response.Text, tools, requests);
}

static string Indent(string text) => string.Join('\n', text.Split('\n').Select(l => "  │ " + l));
static bool Has(string text, params string[] terms) =>
    terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

// 1 — overview
Console.WriteLine("\n▶ 1. Como está a operação das lojas esta semana?");
var (t1, tools1, _) = await Ask([new(ChatRole.User, "Como está a operação das lojas esta semana?")]);
Check(tools1.Contains("GetFreezerStatus") && tools1.Contains("GetWeeklySales") && tools1.Contains("GetCriticalStock"),
    "consultou freezers, vendas e estoque");
Check(Has(t1, "FRZ-07-2", "Campinas", "Cambuí"), "história de Campinas (FRZ-07-2 crítico)");
Check(Has(t1, "Recife", "Boa Viagem", "FRZ-13-1"), "história de Recife (freezer offline + congelados)");
Check(Has(t1, "Curitiba", "Batel"), "história de Curitiba (bebidas + ruptura)");

// 2 — why (calendar + policy)
Console.WriteLine("\n▶ 2. Por que Curitiba vendeu tanta bebida?");
var (t2, _, _) = await Ask([new(ChatRole.User, "Por que Curitiba vendeu tanta bebida?")]);
// The knowledge base may have been searched already in question 1 (same session); what matters is citing the document.
Check(Has(t2, "Festival de Bebidas", "calendário", "calendario-promocoes"), "cita o calendário de promoções");
Check(Has(t2, "2,5", "política de reposição", "politica-reposicao"), "cita a política de reposição (2,5x)");

// 3 — history (minutes)
Console.WriteLine("\n▶ 3. Esse problema de Campinas já era conhecido?");
var (t3, _, _) = await Ask([new(ChatRole.User, "Esse problema de Campinas já era conhecido?")]);
// The minutes passages may have come from question 1's search (same session).
Check(sessionTools.Contains("SearchKnowledgeBase"), "a ata veio da base de conhecimento (nesta sessão)");
Check(Has(t3, "ata", "Comitê"), "cita a ata do Comitê");
Check(Has(t3, "adiad"), "menciona que a troca foi adiada");

// 4 — actions with approval: approve the first request, reject the rest
Console.WriteLine("\n▶ 4. Abre os chamados e os pedidos necessários.");
var (_, _, requests) = await Ask([new(ChatRole.User, "Abre os chamados e os pedidos necessários.")]);
Check(requests.Count > 0, $"pediu aprovação ({requests.Count} pedido(s)) em vez de executar");
Check((await repository.ListActionsAsync()).Count == 0, "nenhuma ação gravada antes do clique");

var approved = 0;
var round = 0;
var lastResponse = "";
while (requests.Count > 0 && round++ < 5)
{
    var responses = new List<AIContent>();
    foreach (var request in requests)
    {
        var call = (FunctionCallContent)request.ToolCall;
        var approve = approved == 0;
        if (approve) approved++;
        Console.WriteLine($"  {(approve ? "👍 APROVAR" : "👎 REJEITAR")} {call.Name}({Arguments(call)})");
        responses.Add(request.CreateResponse(approve, approve ? null : "Rejeitado pelo usuário no dashboard."));
    }
    (lastResponse, _, requests) = await Ask([new(ChatRole.User, responses)]);
}
var actions = await repository.ListActionsAsync();
Check(actions.Count == approved, $"ações gravadas ({actions.Count}) = aprovadas ({approved})");
Check(!Has(lastResponse, "erro do sistema", "falh"), "trata a rejeição como decisão, não como erro");
Check(round <= 2, $"não insistiu nas ações rejeitadas ({round} rodada(s) de aprovação)");

Console.WriteLine(failures == 0 ? "\n✅ Smoke test OK" : $"\n❌ {failures} checagem(ns) falharam");
return failures == 0 ? 0 : 1;

static string Arguments(FunctionCallContent c) =>
    string.Join(", ", (c.Arguments ?? new Dictionary<string, object?>()).Select(kv => $"{kv.Key}: {kv.Value}"));
