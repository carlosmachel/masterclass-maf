using Ipe.Agent.Dashboard;
using Ipe.Agent.Knowledge;
using Ipe.Agent.Security;
using Ipe.Agent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ipe.Agent;

// The stage agent, used by the AG-UI endpoint (Program.cs) and by the console smoke test.
public static class AnalystAgent
{
    public const string Name = "ipe-operations-analyst";

    // Foundry (Azure OpenAI) and, when LocalModel:Model is set, a local model picked on the dashboard.
    public static IChatClient ChatClient(IConfiguration cfg) => ModelSelector.Create(cfg);

    private static ReasoningEffort Effort(IConfiguration? cfg) =>
        Enum.TryParse<ReasoningEffort>(cfg?["AzureOpenAI:ReasoningEffort"], ignoreCase: true, out var e) ? e : ReasoningEffort.Medium;

    public static AIAgent Create(IChatClient chatClient, IServiceProvider sp) =>
        Assemble(Harness(chatClient, sp, Instructions.Analyst, AgentTools.All(sp)), sp);

    // The same analyst for other assistants over MCP: read-only, full answer as text.
    public static AIAgent CreateForMcp(IChatClient chatClient, IServiceProvider sp) =>
        Assemble(Harness(chatClient, sp, Instructions.Analyst + Instructions.ViaMcp, AgentTools.ReadOnly(sp)), sp);

    private static AIAgent Assemble(AIAgent harness, IServiceProvider sp) =>
        new ErrorLoggingAgent(
            new DashboardContextAgent(                       // dashboard filters (AG-UI state) in the context
                harness.AsBuilder()
                    .Use(sp.GetRequiredService<DocumentShield>().InvokeAsync)   // Prompt Shields on documents
                    .Build()),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Ipe.Agent"));

    // CitationFixer goes under function invocation: it sees the search results and the model's raw answer.
    private static AIAgent Harness(IChatClient chatClient, IServiceProvider sp, string instructions, IList<AITool> tools) =>
        new CitationFixer(chatClient).AsHarnessAgent(new HarnessAgentOptions
        {
            Name = Name,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools,
                // AzureOpenAI:ReasoningEffort (default Medium). Low is faster but hurts the analysis.
                Reasoning = new ReasoningOptions { Effort = Effort(sp.GetService<IConfiguration>()) },
            },
            DisableWebSearch = true,           // internal data only
            DisableToolAutoApproval = true,    // no "don't ask again": every action asks for approval
            DisableAgentModeProvider = true,   // no "plan" mode asking before acting
            DisableAgentSkillsProvider = true,
        });
}
