global using Xunit;
using Ipe.Agent.Data;
using Ipe.Agent.Knowledge;

namespace Ipe.Agent.Tests;

public enum Mode { Local, Azure }

// Sources shared by the tests. Azure mode only runs with IPE_SQL set (connection string with
// "Authentication=Active Directory Default"); without it the test is skipped with the reason, it doesn't fail or pass silently.
public static class Sources
{
    public static readonly string DataFolder = Path.Combine(RepositoryRoot(), "data");

    private static readonly Lazy<LocalOperationsRepository> s_local =
        new(() => new LocalOperationsRepository(Path.Combine(DataFolder, "csv")));

    public static readonly Lazy<LocalKnowledgeBase> Knowledge =
        new(() => new LocalKnowledgeBase(Path.Combine(DataFolder, "docs")));

    public static IOperationsRepository Repository(Mode mode)
    {
        if (mode == Mode.Local) return s_local.Value;
        var sql = Environment.GetEnvironmentVariable("IPE_SQL");
        if (string.IsNullOrWhiteSpace(sql)) Assert.Skip("Azure mode: set IPE_SQL to run against Azure SQL.");
        return new SqlOperationsRepository(IpeDbContext.CreateFactory(sql));
    }

    public static IOperationsRepository FreshLocal() => new LocalOperationsRepository(Path.Combine(DataFolder, "csv"));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ipe.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Ipe.slnx) not found.");
    }
}

public static class FoundryIq
{
    // Only runs with IPE_FOUNDRY_IQ (the Search endpoint); without it the test is skipped with the reason.
    public static IKnowledgeBase KnowledgeBase()
    {
        var endpoint = Environment.GetEnvironmentVariable("IPE_FOUNDRY_IQ");
        if (string.IsNullOrWhiteSpace(endpoint)) Assert.Skip("Foundry IQ: set IPE_FOUNDRY_IQ to run against the knowledge base.");
        return new FoundryIqKnowledgeBase(new Uri(endpoint), "ipe-conhecimento", new Azure.Identity.DefaultAzureCredential());
    }
}
