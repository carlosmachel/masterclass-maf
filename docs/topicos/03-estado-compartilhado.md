# Tópico 3 · Estado compartilhado com AG-UI

**Cena 3, o usuário mexe, o agente percebe.** O usuário tira Curitiba do painel pelo olho riscado. Ninguém digita nada, e o agente refaz a análise sem a loja 4.

## O conceito

No AG-UI, cada run carrega um `state`: JSON da aplicação que o cliente manda junto com as mensagens. O tópico 1 mostrou a ida (o servidor manda `STATE_DELTA` para a tela). Esta cena mostra a volta: a tela manda os filtros no `state`, e o servidor lê.

O front guarda `state.filters = { region, excludedStoreIds }` e dispara um run novo quando o filtro muda. A reanálise automática é decisão do front; o agente só recebe um run com estado novo.

## No código: duas portas para o mesmo estado

**Dentro da tool**, o filtro é aplicado de qualquer jeito. `src/Ipe.Agent/Dashboard/DashboardFilters.cs`:

```csharp
// as ChatOptions do run estão no contexto da invocação da tool
public static Filters Current() => Read(FunctionInvokingChatClient.CurrentContext?.Options);

public static Filters Read(ChatOptions? options)
{
    if (!options.TryGetRunAgentInput(out var input)
        || !input.State.TryGetProperty("filters", out var f))
        return Filters.None;
    // o estado vem do cliente: só passam região conhecida e IDs inteiros
    ...
}
```

`TryGetRunAgentInput` dá acesso ao `RunAgentInput` original do AG-UI. Como o estado vem do navegador, ele é tratado como entrada não confiável: região só da lista conhecida, ID só inteiro positivo.

**Antes do run**, o modelo fica sabendo dos filtros. `src/Ipe.Agent/Dashboard/DashboardContextAgent.cs` é um `DelegatingAIAgent`, o decorator de agentes do MAF:

```csharp
public sealed class DashboardContextAgent(AIAgent inner) : DelegatingAIAgent(inner)
{
    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(...) =>
        InnerAgent.RunStreamingAsync(WithFilters(messages, options), session, options, ct);

    // + 1 mensagem de sistema:
    // "Filtros atuais do dashboard: {...}. As tools de dados já aplicam esses filtros;
    //  não analise nem recomende ações para lojas excluídas ou fora da região."
}
```

A frase para o palco: **o modelo sabe dos filtros, e a tool aplica de qualquer jeito.** Não confie no modelo para filtrar dados.

## Armadilha

Na volta de uma aprovação (tópico 4), o `DashboardContextAgent` não injeta nada. A mensagem extra cairia no histórico entre a chamada de tool e o resultado, e a OpenAI recusa com HTTP 400 ("tool_calls must be followed by tool messages"). Os filtros não mudam entre o pedido e a decisão, então não faz falta.

## Para ir além

- [State management com AG-UI (C#)](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/state-management)
- [`DelegatingAIAgent` (referência da API)](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.delegatingaiagent?view=agent-framework-dotnet-latest)
- [Arquitetura do pipeline do agente](https://learn.microsoft.com/agent-framework/concepts/agents/agent-pipeline)
