# Tópico 5 · Middleware contra prompt injection

**Cena 5, o ataque ao vivo.** Um comunicado da distribuidora de bebidas entra na base de conhecimento com uma instrução escondida num comentário HTML (no preview do Markdown, ela não aparece). Com o Prompt Shields, o trecho é bloqueado antes de o modelo ler: o chip da busca ganha o selo vermelho, o modelo recebe um aviso de quarentena e o bloqueio aparece no trace. Sem a defesa, o modelo pode ou não obedecer (o eval do tópico 7 mede isso), e essa incerteza é o motivo de a defesa ficar no código.

## O ataque: prompt injection indireta

Na injeção direta, o usuário tenta enganar o modelo. Na indireta, o texto malicioso vem de uma fonte que o agente consulta: documento, e-mail, página web, resultado de tool. O modelo não distingue "dado" de "instrução", e um agente com tools de ação transforma isso em ação.

O arquivo do ataque é `data/attack/comunicado-distribuidora-bebidas.md`. Ele entra na base de conhecimento só durante a cena e sai logo depois.

## A defesa: Prompt Shields entre a tool e o modelo

Prompt Shields (Azure AI Content Safety) analisa texto e devolve `attackDetected` para o prompt do usuário e para cada documento. A demo chama a API REST `text:shieldPrompt` com token Entra (`src/Ipe.Agent/Security/PromptShields.cs`); não há SDK .NET estável para essa chamada.

O ponto certo para chamar é um **middleware de function calling** do MAF: ele roda em volta de cada execução de tool e vê o resultado antes do modelo.

`src/Ipe.Agent/AnalystAgent.cs`:

```csharp
harness.AsBuilder()
    .Use(sp.GetRequiredService<DocumentShield>().InvokeAsync)
    .Build()
```

`src/Ipe.Agent/Security/DocumentShield.cs`:

```csharp
public async ValueTask<object?> InvokeAsync(AIAgent agent, FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken ct)
{
    var result = await next(context, ct);                         // a tool roda
    if (context.Function.Name != "SearchKnowledgeBase") return result;

    var attacks = await shields.DetectDocumentAttacksAsync(question, texts, ct);

    // trecho com ataque vira aviso de quarentena: o modelo nunca lê a instrução
    Activity.Current?.AddEvent(new("prompt_shields.attack_detected", ...));
    return passages.Select((p, i) => attacks[i] ? p with { Text = Quarantine, Blocked = true } : p).ToList();
}
```

- **O ataque não chega ao contexto.** O modelo recebe "[Trecho bloqueado pelo Prompt Shields...]" no lugar do texto.
- **Fica no trace.** O evento `prompt_shields.attack_detected` aparece no span `execute_tool` do Aspire, e o chip "Base de conhecimento" ganha o selo vermelho na tela.

## Defesa em camadas

O Shields não é a única trava. Mesmo sem ele, o pedido de R$ 1,2 mi para no card de aprovação (tópico 4). O eval (tópico 7) mostra que o `gpt-5-mini` resiste mais que o 8B, mas cai 2 em 3 rodadas sem o Shields: não dá para apostar no modelo.

O botão "Shields" da tela só existe na demo (`Security:AllowDashboardToggle`). Em produção a defesa fica ligada e o cliente não consegue desligar.

## Para ir além

- [Agent middleware (C#)](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/)
- [Criar middleware de function calling](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/defining-middleware#step-4-create-function-calling-middleware)
- [Prompt Shields (conceito)](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/jailbreak-detection)
- [Quickstart do Prompt Shields (REST)](https://learn.microsoft.com/azure/ai-services/content-safety/quickstart-jailbreak)
- [Defesa contra prompt injection indireta (Zero Trust)](https://learn.microsoft.com/security/zero-trust/sfi/defend-indirect-prompt-injection)
