# Tópico 4 · Aprovação humana de tools

**Cena 4, humano no comando.** "Abre os chamados e os pedidos necessários." O agente propõe as ações, os cards chegam na bandeja, o usuário aprova um e rejeita os outros. Só o aprovado é executado.

## O conceito

Human-in-the-loop no MAF é por tool. Uma tool embrulhada em `ApprovalRequiredAIFunction` não roda quando o modelo a chama: o run termina com um `ToolApprovalRequestContent`, e quem chamou o agente decide. A decisão volta num run novo, na mesma sessão, como `ToolApprovalResponseContent`.

## No código: uma linha separa ler de agir

`src/Ipe.Agent/Tools/AgentTools.cs`:

```csharp
// leitura: roda na hora
AIFunctionFactory.Create(operations.GetFreezerStatusAsync, "GetFreezerStatus"),

// ação: só roda depois do "Aprovar"
new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(actions.OpenMaintenanceTicketAsync, "OpenMaintenanceTicket")),
new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(actions.CreateEmergencyOrderAsync, "CreateEmergencyOrder")),
```

```csharp
// Program.cs: a aprovação precisa voltar para a mesma sessão
analyst.WithInMemorySessionStore(withIsolation: false);
```

O caminho completo:

```text
modelo chama OpenMaintenanceTicket(...)
  → MAF pausa: ToolApprovalRequestContent
  → AG-UI: interrupt → card Aprovar / Rejeitar
  → front manda o resume (approved + a tool call original), mesmo threadId
  → MAF executa ou devolve "rejected" ao modelo
```

No front, `web/src/agui/approval.ts` monta o resume com `approved` e a tool call que gerou o interrupt (nome e argumentos que chegaram antes, nos eventos `TOOL_CALL_START`/`TOOL_CALL_ARGS`).

## Decisões da demo

- **`DisableToolAutoApproval = true` no Harness** (tópico 1). O Harness oferece "aprovar sempre" (`ToolApprovalAgent`); aqui toda ação pede aprovação, sempre.
- **Rejeitar é resposta, não erro.** O modelo recebe o resultado de rejeição e segue. As instruções dizem para não pedir de novo uma ação rejeitada.
- **Ação aprovada vai para o painel.** `MapResult("OpenMaintenanceTicket", ...)` põe a confirmação no card "Ações executadas". Rejeitada não tem `ActionConfirmation`, então nada aparece.
- **Validação no servidor.** `ActionTools` confere prioridade e freezer antes de gravar. A aprovação humana não substitui a validação dos argumentos.

## Para ir além

- [Function tools com aprovação humana (C#)](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval)
- [Human-in-the-loop com AG-UI](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/human-in-the-loop)
- [`ToolApprovalAgent` e o "não perguntar de novo"](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.toolapprovalagent?view=agent-framework-dotnet-latest)
