using Ipe.Agent.Data;
using Ipe.Agent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.OnCall;

// Refrigeration on-call: a MAF workflow triggered by an alarm (not by a question).
//   Alarm → Triage (code) → On-call agent (LLM + tools; the plan asks for approval) → Closing (code)
// The on-call agent negotiates the restock with the DC Southeast agent over A2A (DistributionCenterClient.cs).
// A single approval for the whole plan (ContingencyPlan.cs).
// Only the middle step uses a model. Triage and closing are deterministic: not everything needs an LLM.
public static class OnCallWorkflow
{
    public const string Name = "refrigeration-on-call";

    public static Workflow Create(IChatClient chatClient, IServiceProvider sp)
    {
        var repository = sp.GetRequiredService<IOperationsRepository>();
        var agent = OnCallAgent(chatClient, sp);

        var triage = new Triage(repository).BindExecutor();
        var closing = new Closing().BindExecutor();
        var onCall = agent.BindAsExecutor(new AIAgentHostOptions { EmitAgentUpdateEvents = true });

        return new WorkflowBuilder(triage)
            .WithName(Name)
            .WithDescription("Temperature alarm → triage → on-call agent → human approval → closing")
            .AddEdge(triage, onCall)
            .AddEdge(onCall, closing)
            .WithOutputFrom(triage, closing)
            .WithOpenTelemetry()      // workflow and per-executor spans in Aspire
            .Build();
    }

    private static AIAgent OnCallAgent(IChatClient chatClient, IServiceProvider sp)
    {
        var operations = ActivatorUtilities.CreateInstance<OperationsTools>(sp);
        var actions = ActivatorUtilities.CreateInstance<ActionTools>(sp);
        var dc = sp.GetRequiredService<DistributionCenterClient>();
        var contingency = new ContingencyPlan(actions);
        return chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Id = "OnCallAgent",
            Name = "on-call-agent",
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    Você é o agente de plantão de refrigeração da Rede Ipê. Recebe um alarme já confirmado pela triagem.
                    Para cada freezer crítico, NESTA ordem:
                    1. Veja o histórico de temperatura (2 dias) e os chamados recentes; diga em uma frase o diagnóstico.
                    2. Peça ao CD Sudeste, com RequestDcProposal, a reposição de Congelados para a loja do freezer:
                       150 caixas (a carga de um freezer), o quanto antes. Só para lojas do Sudeste. Espere a resposta.
                    3. Chame ExecuteContingencyPlan uma vez, com o diagnóstico e a proposta do CD (número e entregas).
                       Não pergunte em texto "posso executar?": chamar a tool JÁ É o pedido de aprovação (o humano
                       decide no painel o plano inteiro: chamado ALTA + pedido emergencial).
                    Só não execute o plano se houver chamado com status Aberto para o mesmo freezer; Fechado não conta.
                    O resultado "Tool call invocation rejected." quer dizer que o humano clicou em Rejeitar: é decisão
                    dele, não erro. Nunca peça de novo uma ação rejeitada. Depois da decisão, diga em até 3 linhas o que foi
                    aberto (com os números) ou que nada foi executado, por decisão do humano.
                    """,
                Tools =
                [
                    AIFunctionFactory.Create(operations.GetTemperatureHistoryAsync, "GetTemperatureHistory"),
                    AIFunctionFactory.Create(operations.ListMaintenanceTicketsAsync, "ListMaintenanceTickets"),
                    AIFunctionFactory.Create(dc.RequestProposalAsync, "RequestDcProposal"),
                    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(contingency.ExecuteAsync, "ExecuteContingencyPlan")),
                ],
                Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
            },
        }).AsBuilder().UseOpenTelemetry().Build();
    }
}

// 1. Triage: checks the alarm against the data. With no critical freezer, it ends without spending tokens.
// ChatProtocolExecutor: the start of a workflow exposed as an agent receives messages + TurnToken.
internal sealed class Triage(IOperationsRepository repository)
    : ChatProtocolExecutor("Triage", new ChatProtocolExecutorOptions { AutoSendTurnToken = false })
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol) =>
        base.ConfigureProtocol(protocol).SendsMessage<ChatMessage>().SendsMessage<TurnToken>().YieldsOutput<string>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> alarm, IWorkflowContext ctx, bool? emitEvents, CancellationToken ct = default)
    {
        // On an approval resume the host also delivers the response here; it continues through the workflow's port.
        // No alarm text, no new turn.
        var text = string.Join(" ", alarm.Where(m => m.Role == ChatRole.User).Select(m => m.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        if (text.Length == 0) return;

        var critical = (await repository.GetFreezerStatusAsync(null, [], ct)).Where(f => f.Status == "CRITICO").ToList();
        if (critical.Count == 0)
        {
            await ctx.YieldOutputAsync("Triagem: nenhum freezer crítico. Alarme descartado sem acionar o agente.", ct);
            return;
        }
        var list = string.Join("\n", critical.Select(f =>
            $"- {f.FreezerId} ({f.Store}, {f.City}): máx. 24h {f.Max24h:F1} °C, {f.HoursAboveMinus15} h acima de -15 °C"));
        await ctx.SendMessageAsync(new ChatMessage(ChatRole.User,
            $"Alarme recebido: {text}\nFreezers críticos confirmados pela triagem:\n{list}"), ct);
        await ctx.SendMessageAsync(new TurnToken(emitEvents), ct);
    }
}

// 3. Closing: wraps up the on-call shift with what the agent did (no LLM).
internal sealed class Closing() : ChatProtocolExecutor("Closing", new ChatProtocolExecutorOptions { AutoSendTurnToken = false })
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol) =>
        base.ConfigureProtocol(protocol).YieldsOutput<string>();

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages, IWorkflowContext ctx, bool? emitEvents, CancellationToken ct = default)
    {
        var results = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? "").ToList();
        var tickets = results.Count(r => r.Contains("Chamado "));
        var orders = results.Count(r => r.Contains("Pedido emergencial "));
        await ctx.YieldOutputAsync($"Plantão encerrado: {tickets} chamado(s) e {orders} pedido(s) emergencial(is).", ct);
    }
}
