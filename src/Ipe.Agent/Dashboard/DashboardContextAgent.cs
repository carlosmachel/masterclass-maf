using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Dashboard;

// Input side of the shared state: before each run, tells the model which filters the dashboard has.
// Only the filters go into the context; the agent produced the KPIs and charts itself and doesn't need them back.
public sealed class DashboardContextAgent(AIAgent inner) : DelegatingAIAgent(inner)
{
    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null,
        CancellationToken cancellationToken = default) =>
        InnerAgent.RunAsync(WithFilters(messages, options), session, options, cancellationToken);

    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null,
        CancellationToken cancellationToken = default) =>
        InnerAgent.RunStreamingAsync(WithFilters(messages, options), session, options, cancellationToken);

    private static IEnumerable<ChatMessage> WithFilters(IEnumerable<ChatMessage> messages, AgentRunOptions? options)
    {
        if (options is not ChatClientAgentRunOptions { ChatOptions: { } chatOptions }) return messages;

        // Approval resume: inject nothing. The extra message would land in the history between the assistant with
        // the tool calls and the results, and OpenAI rejects it (HTTP 400 "tool_calls must be followed by tool messages").
        // The filters don't change between the request and the decision.
        if (messages.Any(m => m.Contents.Any(c => c is ToolApprovalResponseContent))) return messages;

        var filters = DashboardFilters.Read(chatOptions);
        var description = JsonSerializer.Serialize(new { region = filters.Region, excludedStoreIds = filters.ExcludedStoreIds });
        return
        [
            new ChatMessage(ChatRole.System,
                $"Filtros atuais do dashboard: {description}. As tools de dados já aplicam esses filtros; " +
                "não analise nem recomende ações para lojas excluídas ou fora da região."),
            .. messages,
        ];
    }
}
