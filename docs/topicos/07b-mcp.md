# Tópico 7b · Agente como servidor MCP

**Cena 7b, o agente fora da tela.** No terminal, o Claude Code pergunta à Rede Ipê se o problema do freezer de Campinas já era conhecido. Quem responde é o mesmo analista da tela, agora como tool MCP.

## O conceito

MCP (Model Context Protocol) é o padrão para expor tools a assistentes: Claude Code, VS Code, outros agentes. AG-UI é para a tela; MCP é para outros assistentes. O MAF transforma um agente inteiro numa função com `.AsAIFunction()`, e o SDK oficial de MCP em C# publica essa função como tool.

## No código

`src/Ipe.Agent/Mcp/McpRegistration.cs`:

```csharp
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true);

builder.Services.AddSingleton(sp => McpServerTool.Create(
    AnalystAgent.CreateForMcp(chatClient, sp)          // só leitura, sem ações
        .AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "ask_analyst",
            Description = "Analista de operações da Rede Ipê...",
        })));
```

```csharp
// Program.cs
app.MapMcp("/mcp");
```

No Claude Code, o `.mcp.json` na raiz do repo aponta para `http://localhost:4280/api/mcp` (proxy do Vite).

## As decisões de segurança

- **Só leitura.** `CreateForMcp` usa `AgentTools.ReadOnly`, que tira `PublishAlerts`, `OpenMaintenanceTicket` e `CreateEmergencyOrder`. Por MCP não há tela nem botão de aprovar, então as ações ficam no Centro de Operações.
- **Mesmo Harness, mesmo Shields.** O agente do MCP passa pelo mesmo `Assemble` do palco, com o `DocumentShield`.
- **A base de conhecimento não sai direto.** As consultas de dados (`GetFreezerStatus`, `GetWeeklySales`...) também são expostas como tools MCP soltas, mas `SearchKnowledgeBase` não: os documentos só saem pelo agente, que passa cada trecho pelo Prompt Shields.

## Para ir além

- [Usar e expor MCP com o Agent Framework (C#)](https://learn.microsoft.com/agent-framework/agents/tools/local-mcp-tools#exposing-an-agent-as-an-mcp-server)
- [SDK oficial de MCP para C#](https://github.com/modelcontextprotocol/csharp-sdk)
- [Boas práticas de segurança do MCP](https://modelcontextprotocol.io/specification/draft/basic/security_best_practices)
