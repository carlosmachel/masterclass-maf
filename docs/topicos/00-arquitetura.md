# Tópico 0 · A arquitetura da demo

**Cena 0, abertura.** A Rede Ipê tem 20 lojas e três problemas escondidos nos dados. Antes da primeira pergunta, vale mostrar as peças que vão aparecer na sessão.

## As peças

```text
navegador (Vue 3 + PrimeVue)
   │  AG-UI: HTTP POST + SSE
   ▼
Ipe.Agent (ASP.NET Core)
   ├─ "/"        analista de operações (Harness + tools)
   ├─ "/oncall"  plantão (workflow que vira agente)
   └─ "/mcp"     o mesmo analista para Claude Code, VS Code...
        │
        ├─ SQL (dados das lojas) ou CSV no modo Local
        ├─ Foundry IQ (documentos) ou pasta data/docs no modo Local
        ├─ Prompt Shields (Content Safety)
        └─ A2A ──► Ipe.DistributionCenter (outro processo, outra "empresa")
```

O Aspire (`src/Ipe.AppHost/Program.cs`) sobe tudo junto: o agente, o agente do CD na porta 5180 e o front na porta 4280. O painel do Aspire (`localhost:15000`) mostra os traces das cenas 5 e 6.

## O Program.cs cabe numa tela

`src/Ipe.Agent/Program.cs` é curto de propósito, porque aparece no palco. Ele registra o agente, o plantão e o MCP, e mapeia três endpoints:

```csharp
var analyst = builder.AddAIAgent(AnalystAgent.Name, (sp, _) =>
    AnalystAgent.Create(AnalystAgent.ChatClient(builder.Configuration), sp));
analyst.WithInMemorySessionStore(withIsolation: false);

app.MapAGUIServer(analyst, "/").WithMetadata(DashboardMapping.Options());
app.MapAGUIServer(onCall, "/oncall").WithMetadata(DashboardMapping.Options());
app.MapMcp("/mcp");
```

A lógica fica nas pastas `Tools/`, `Data/`, `Knowledge/`, `Dashboard/`, `OnCall/` e `Security/`.

## Por que AG-UI

AG-UI é um protocolo de eventos (SSE) entre o agente e a tela: texto em streaming, chamadas de tool, estado compartilhado e pedidos de aprovação. O front da demo fala AG-UI com um cliente feito à mão em `web/src/agui/`, sem CopilotKit. Assim dá para mostrar que o protocolo é simples: um POST e uma lista de eventos.

## Credenciais e modelos

- `DefaultAzureCredential` em tudo, sem chave no repo. No AppHost, `AZURE_TOKEN_CREDENTIALS=dev` restringe às credenciais de desenvolvedor (`az login`) e evita a tentativa de managed identity.
- Dois modelos: Foundry (`gpt-5-mini`) e um local via Ollama (`qwen3-ipe-8b`), escolhidos no seletor da tela (`ModelSelector.cs`). O modelo local é o que cai no ataque da cena 5.

## Para ir além

- [Microsoft Agent Framework: visão geral](https://learn.microsoft.com/agent-framework/overview/)
- [Integração AG-UI com o Agent Framework (C#)](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/)
- [Protocolo AG-UI](https://docs.ag-ui.com/introduction)
