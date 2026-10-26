global using Xunit;
using Microsoft.Extensions.Configuration;

namespace Ipe.Agent.Evals;

public enum Model { Foundry, Local }

// Eval configuration: the agent's user-secrets (AzureOpenAI:*) and the tenant pinned in the AppHost (Azure:TenantId).
// The eval calls real models (slow and paid): it only runs with IPE_EVAL=1; otherwise it's skipped.
public static class EvalEnvironment
{
    public static readonly string Root = RepositoryRoot();
    public static readonly string ResultsFolder = Path.Combine(Root, "demo", "eval", "results");
    public static readonly string Report = Path.Combine(Root, "demo", "eval", "report.html");

    public static readonly IConfiguration Cfg = Create();

    public static void RequireEval()
    {
        if (Environment.GetEnvironmentVariable("IPE_EVAL") != "1")
            Assert.Skip("The eval calls real models: run it with IPE_EVAL=1 dotnet test --project src/Ipe.Agent.Evals.");
    }

    private static IConfiguration Create()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalModel:Model"] = "qwen3-ipe-8b",   // same as the AppHost
                ["LocalModel:TimeoutSeconds"] = "600",   // Ollama queues the parallel cases; 100 s ran out on 20/10
            })
            .AddUserSecrets("ipe-agent-masterclass")
            .AddUserSecrets("ipe-apphost-masterclass")
            .AddEnvironmentVariables()
            .Build();
        // As in the AppHost: the az CLI may be signed in to another tenant than the demo resources.
        if (cfg["Azure:TenantId"] is { Length: > 0 } tenant && Environment.GetEnvironmentVariable("AZURE_TENANT_ID") is null)
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", tenant);
        Environment.SetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS", "dev");
        return cfg;
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ipe.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Ipe.slnx) not found.");
    }
}
