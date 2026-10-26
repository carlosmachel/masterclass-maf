using Ipe.Agent.Data;
using Ipe.Agent.Knowledge;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ipe.Agent.Evals;

// The same stage agent (AnalystAgent.Create), with a fixed model and Prompt Shields setting and Local mode data
// (data/csv + data/docs): the eval doesn't depend on SQL or Foundry IQ and writes nothing outside memory.
public sealed class EvalAgent
{
    private readonly AIAgent _agent;
    private AgentSession? _session;

    public EvalAgent(Model model, bool promptShields, bool withAttack = false)
    {
        var cfg = new ConfigurationBuilder()
            .AddConfiguration(EvalEnvironment.Cfg)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:PromptShields"] = promptShields.ToString(),
                ["Security:AllowDashboardToggle"] = "false",
            })
            .Build();

        var builder = Host.CreateEmptyApplicationBuilder(new());
        builder.Configuration.AddConfiguration(cfg);
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IOperationsRepository>(new LocalOperationsRepository(Path.Combine(EvalEnvironment.Root, "data", "csv")));
        builder.Services.AddSingleton<IKnowledgeBase>(new LocalKnowledgeBase(DocsFolder(withAttack)));
        builder.AddSecurity();
        var sp = builder.Build().Services;

        var chatClient = model == Model.Local
            ? ModelSelector.Local(cfg) ?? throw new InvalidOperationException("LocalModel:Model is not configured.")
            : ModelSelector.Foundry(cfg);
        _agent = AnalystAgent.Create(chatClient, sp);
    }

    // One turn: the ChatResponse carries text, tool calls and approval requests (ToolApprovalRequestContent).
    public async Task<ChatResponse> AskAsync(string question, CancellationToken ct)
    {
        _session ??= await _agent.CreateSessionAsync(ct);
        var response = await _agent.RunAsync([new ChatMessage(ChatRole.User, question)], _session, cancellationToken: ct);
        return new ChatResponse([.. response.Messages]);
    }

    // Red-team knowledge base: the usual documents plus the poisoned notice (data/attack).
    private static string DocsFolder(bool withAttack)
    {
        var docs = Path.Combine(EvalEnvironment.Root, "data", "docs");
        if (!withAttack) return docs;
        var folder = Directory.CreateTempSubdirectory("ipe-eval-docs-").FullName;
        foreach (var file in Directory.EnumerateFiles(docs, "*.md"))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        File.Copy(Path.Combine(EvalEnvironment.Root, "data", "attack", RedTeam.Document), Path.Combine(folder, RedTeam.Document));
        return folder;
    }
}
