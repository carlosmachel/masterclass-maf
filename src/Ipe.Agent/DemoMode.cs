using Azure.Identity;
using Ipe.Agent.Data;
using Ipe.Agent.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace Ipe.Agent;

// Demo:Mode = Azure (Azure SQL + Foundry IQ) or Local (data/csv + data/docs). The tools only see the interfaces.
public static class DemoMode
{
    public static IHostApplicationBuilder AddDemoSources(this IHostApplicationBuilder builder)
    {
        var mode = builder.Configuration["Demo:Mode"] ?? "Azure";
        var dataFolder = Path.GetFullPath(builder.Configuration["Demo:DataFolder"]
            ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "data"));

        if (mode.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IOperationsRepository>(_ => new LocalOperationsRepository(Path.Combine(dataFolder, "csv")));
            builder.Services.AddSingleton<IKnowledgeBase>(_ => new LocalKnowledgeBase(Path.Combine(dataFolder, "docs")));
        }
        else
        {
            var sql = builder.Configuration.GetConnectionString("Ipe")
                ?? throw new InvalidOperationException("Demo:Mode=Azure requires ConnectionStrings:Ipe (Authentication=Active Directory Default).");
            builder.Services.AddPooledDbContextFactory<IpeDbContext>(o => IpeDbContext.Configure(o, sql));
            builder.Services.AddSingleton<IOperationsRepository, SqlOperationsRepository>();

            // Foundry IQ (knowledge base on Azure AI Search). Without an endpoint, falls back to the local documents.
            var foundryIq = builder.Configuration["FoundryIQ:Endpoint"];
            builder.Services.AddSingleton<IKnowledgeBase>(_ => string.IsNullOrWhiteSpace(foundryIq)
                ? new LocalKnowledgeBase(Path.Combine(dataFolder, "docs"))
                : new FoundryIqKnowledgeBase(new Uri(foundryIq),
                    builder.Configuration["FoundryIQ:KnowledgeBase"] ?? "ipe-conhecimento", new DefaultAzureCredential()));
        }
        return builder;
    }
}
