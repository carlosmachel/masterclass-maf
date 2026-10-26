using Ipe.Agent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Ipe.Agent;

// MCP: the analyst becomes a tool for any MCP client (Claude Code, VS Code...). Same Harness, same
// tools and same Prompt Shields; but no dashboard and no actions (human approval stays in the Operations Center).
// In Claude Code: .mcp.json at the repo root points to http://localhost:4280/api/mcp (Vite proxy).
public static class McpRegistration
{
    // Queries exposed directly, besides the agent. The knowledge base is left out on purpose: it only goes out
    // through the agent, which runs every passage through Prompt Shields (DocumentShield).
    private static readonly string[] DirectQueries =
        ["GetFreezerStatus", "GetWeeklySales", "GetCriticalStock", "GetTemperatureHistory", "ListMaintenanceTickets"];

    public static IHostApplicationBuilder AddAnalystMcp(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true);

        // The MCP server reads McpServerTool from DI; with a factory, the agent is built with the ready IServiceProvider.
        builder.Services.AddSingleton(sp => McpServerTool.Create(
            AnalystAgent.CreateForMcp(AnalystAgent.ChatClient(sp.GetRequiredService<IConfiguration>()), sp)
                .AsAIFunction(new AIFunctionFactoryOptions
                {
                    Name = "ask_analyst",
                    Description = """
                        Analista de operações da Rede Ipê Supermercados (agente do Microsoft Agent Framework).
                        Responde perguntas sobre freezers, vendas, estoque e chamados das lojas, cruzando os dados com
                        a base de conhecimento (atas, manuais, políticas) e citando os documentos. Só consulta: não
                        abre chamados nem pedidos. Faça uma pergunta completa em português.
                        """,
                })));
        foreach (var name in DirectQueries)
            builder.Services.AddSingleton(sp =>
                McpServerTool.Create((AIFunction)AgentTools.ReadOnly(sp).Single(t => t.Name == name)));

        return builder;
    }
}
