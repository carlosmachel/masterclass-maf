# Tópico 6 · Workflows e A2A

**Cena 6, plantão.** Um alarme de IoT (o botão "Alarme IoT" faz esse papel no palco) acorda o agente de plantão sem ninguém perguntar nada. A triagem confirma o freezer crítico, o agente pede reposição ao agente do Centro de Distribuição por A2A e monta um plano de contingência, que o humano aprova de uma vez.

## Parte 1: workflow, porque nem tudo precisa ser LLM

Um workflow do MAF é um grafo de **executors** ligados por **edges**. Cada executor pode ser código comum ou um agente. Aqui só o passo do meio usa modelo:

```text
alarme IoT → Triage (código) → agente de plantão (LLM + tools + A2A) → aprovação humana → Closing (código)
```

`src/Ipe.Agent/OnCall/OnCallWorkflow.cs`:

```csharp
var triage  = new Triage(repository).BindExecutor();     // ChatProtocolExecutor
var closing = new Closing().BindExecutor();
var onCall  = agent.BindAsExecutor(new AIAgentHostOptions { EmitAgentUpdateEvents = true });

return new WorkflowBuilder(triage)
    .AddEdge(triage, onCall)
    .AddEdge(onCall, closing)
    .WithOutputFrom(triage, closing)
    .WithOpenTelemetry()      // um span por executor no Aspire
    .Build();
```

- **Triagem sem token.** Sem freezer crítico, `Triage` encerra com `YieldOutputAsync` e o modelo nem é chamado.
- **`ChatProtocolExecutor`.** O executor de entrada de um workflow exposto como agente recebe mensagens e um `TurnToken`. A triagem só manda o `TurnToken` adiante quando há trabalho para o agente.

### O workflow vira agente

`OnCallRegistration.cs` chama `.AsAIAgent(...)` sobre o workflow, e o `Program.cs` mapeia como qualquer agente:

```csharp
app.MapAGUIServer(onCall, "/oncall").WithMetadata(DashboardMapping.Options());
```

O front não sabe se do outro lado tem um agente ou um workflow. O protocolo é o mesmo.

### Uma aprovação para o plano inteiro

`ContingencyPlan.ExecuteAsync` abre o chamado ALTA e cria o pedido emergencial numa só tool com `ApprovalRequiredAIFunction`. Duas aprovações seguidas dentro do workflow não funcionaram na versão 1.24 preview (em paralelo o resume não continua; em sequência o agente encerra o turno depois da primeira).

## Parte 2: A2A, o agente de outra empresa vira tool

A2A (Agent-to-Agent) é um protocolo aberto para agentes de sistemas diferentes conversarem. Cada agente publica um **Agent Card** em `/.well-known/agent-card.json` com nome, skills, exemplos e endpoints.

**Servidor**, `src/Ipe.DistributionCenter/Program.cs` (outro processo, a "Ipê Logística"):

```csharp
var dc = builder.AddAIAgent(DcAgent.Name, (sp, _) => DcAgent.Create(cfg, sp));
dc.AddA2AServer();

app.MapA2AHttpJson(dc, "/a2a/dc-southeast");
app.MapWellKnownAgentCard(DcAgent.Card(url));   // → /.well-known/agent-card.json
```

**Cliente**, `src/Ipe.Agent/OnCall/DistributionCenterClient.cs`:

```csharp
_agent = new A2ACardResolver(new Uri(url), Http).GetAIAgentAsync(Http, cancellationToken: ct);

[Description("Pede reposição ao agente do CD Sudeste (outro sistema, via protocolo A2A)...")]
public async Task<string> RequestProposalAsync(string request, CancellationToken ct = default)
{
    var dc = await Agent(ct);
    return (await dc.RunAsync(request, cancellationToken: ct)).Text;
}
```

- O agente remoto é um `AIAgent` como qualquer outro. Embrulhado num método com `[Description]`, ele vira tool do agente de plantão.
- **A proposta não é pedido.** O CD só devolve uma proposta (PRP-SE-41xx); o pedido continua com aprovação humana na loja.
- O trace atravessa os dois processos no Aspire, pela instrumentação do `HttpClient`.
- O cliente tem `HttpClient` próprio com timeout de 2 minutos: a resiliência padrão do ServiceDefaults corta em 10 s, e o agente do CD responde em 10 a 20 s.

## Para ir além

- [Workflow Builder e execução (C#)](https://learn.microsoft.com/agent-framework/concepts/workflows/builder-and-execution)
- [Edges](https://learn.microsoft.com/agent-framework/concepts/workflows/edges)
- [Agent executor e `AIAgentHostOptions`](https://learn.microsoft.com/agent-framework/concepts/workflows/advanced/agent-executor)
- [Workflows como agentes (`AsAIAgent`)](https://learn.microsoft.com/agent-framework/workflows/as-agents)
- [Hospedar agentes com A2A (C#)](https://learn.microsoft.com/agent-framework/hosting/self-hosting/a2a/dotnet)
- [Consumir um agente A2A (`A2ACardResolver`)](https://learn.microsoft.com/agent-framework/integrations/by-component/agent-services/a2a)
- [Especificação do A2A](https://a2a-protocol.org/latest/)
