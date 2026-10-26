# Tópico 1 · Criar um agente Harness

**Cena 1, o dashboard nasce sozinho.** Uma pergunta ("Como está a operação das lojas esta semana?") e o painel se monta com KPIs, gráficos e alertas. Três peças fazem isso: o agente Harness, as tools e o mapeamento do resultado das tools para a tela.

## 1. O agente: `AsHarnessAgent`

Um `IChatClient` (o cliente do modelo, do `Microsoft.Extensions.AI`) vira agente com uma chamada. O Harness é o agente "com bateria inclusa" do MAF: loop de function calling, histórico por sessão, streaming, telemetria, aprovação de tools e, por padrão, web search, skills e modo plano.

`src/Ipe.Agent/AnalystAgent.cs`:

```csharp
new CitationFixer(chatClient).AsHarnessAgent(new HarnessAgentOptions
{
    Name = Name,
    ChatOptions = new ChatOptions
    {
        Instructions = instructions,
        Tools = tools,
        Reasoning = new ReasoningOptions { Effort = Effort(...) },
    },
    DisableWebSearch = true,           // só dados internos
    DisableToolAutoApproval = true,    // sem "não perguntar de novo": toda ação pede aprovação
    DisableAgentModeProvider = true,   // sem modo "plano"
    DisableAgentSkillsProvider = true,
});
```

A decisão que vale discutir: cada `Disable*` é uma escolha de segurança. O Harness vem pronto para tarefas longas e abertas; um agente de operações precisa de menos coisa ligada. Sem `DisableWebSearch`, o Harness adiciona um `HostedWebSearchTool` e o agente poderia trazer conteúdo de fora para dentro da análise.

`CitationFixer` é um `IChatClient` que fica embaixo do Harness e troca nomes de documento citados errado pelo nome real (tópico 2). `ErrorLoggingAgent` e `DashboardContextAgent` embrulham o agente por fora (tópico 3).

## 2. As tools: um método C# vira tool

`src/Ipe.Agent/Tools/OperationsTools.cs` tem métodos comuns com `[Description]`. `AIFunctionFactory.Create` lê a assinatura e gera o JSON schema que o modelo recebe:

```csharp
[Description("""
    Status atual de todos os freezers de congelados da rede...
    Use primeiro em qualquer pergunta sobre a operação ou sobre refrigeração.
    """)]
public async Task<IReadOnlyList<FreezerStatus>> GetFreezerStatusAsync(
    [Description("Região para filtrar... Omita para todas.")] string? region = null,
    [Description("IDs de lojas que o usuário removeu...")] int[]? excludedStoreIds = null,
    CancellationToken ct = default)
```

```csharp
// src/Ipe.Agent/Tools/AgentTools.cs
AIFunctionFactory.Create(operations.GetFreezerStatusAsync, "GetFreezerStatus"),
```

Dois pontos para o palco:

- A `Description` é o prompt da tool. Ela diz **quando** usar, não só o que faz, e fica em português porque o modelo conversa em português.
- O retorno já vem filtrado. `GetFreezerStatus` devolve só os freezers fora da faixa: o modelo precisa dos problemas, não de 45 linhas "OK".

## 3. O painel: o resultado da tool desenha a tela

O modelo não gera a tela. Quando uma tool termina, o servidor transforma o resultado num `STATE_DELTA` (JSON Patch) do AG-UI, e o Vue aplica no estado do painel.

`src/Ipe.Agent/Dashboard/DashboardMapping.cs`:

```csharp
public static AGUIStreamOptions Options() => new AGUIStreamOptions()
    .MapCall("GetFreezerStatus", c => Start(c, "Analisando freezers"))          // abre um passo "rodando"
    .MapResult("GetFreezerStatus", r => Delta(r,
        (IReadOnlyList<FreezerStatus> f) => [("/kpis/freezers", FreezerKpi(f))],  // vira KPI
        f => $"{f.Count} fora da faixa"));                                         // fecha o passo
```

```csharp
// Program.cs
app.MapAGUIServer(analyst, "/").WithMetadata(DashboardMapping.Options());
```

O fluxo: `FunctionResultContent` → `MapResult` → `STATE_DELTA` no SSE → KPI, gráfico ou alerta no Vue.

- **Zero token:** o modelo não gasta saída desenhando a tela.
- **Determinístico:** o progresso ("Analisando freezers…") não depende de o modelo lembrar de avisar.

O mapeamento é opt-in: resultado de tool não vira estado sozinho. Só as tools listadas em `Options()` mexem no painel.

## Para ir além

- [Agent Harness (conceito, C#)](https://learn.microsoft.com/agent-framework/concepts/harness)
- [Primeiros passos com o Harness](https://learn.microsoft.com/agent-framework/get-started/harness)
- [Function tools e uso com o Harness](https://learn.microsoft.com/agent-framework/agents/tools/function-tools#use-function-tools-with-harness-agent)
- [Backend tool rendering com AG-UI](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/backend-tool-rendering)
- [State management com AG-UI (`MapCall`/`MapResult`)](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/state-management)
