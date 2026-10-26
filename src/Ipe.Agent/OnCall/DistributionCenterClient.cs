using System.ComponentModel;
using A2A;
using Microsoft.Agents.AI;

namespace Ipe.Agent.OnCall;

// A2A: the DC Southeast agent is another process (src/Ipe.DistributionCenter), from another "company".
// Discovered through its Agent Card (/.well-known/agent-card.json); the remote agent becomes a tool of the on-call agent.
public sealed class DistributionCenterClient(IConfiguration cfg)
{
    // Own HttpClient: the ServiceDefaults standard resilience cuts attempts at 10 s, and the DC agent
    // answers in 10–20 s. The trace still crosses the processes (HttpClient instrumentation).
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private Task<AIAgent>? _agent;

    // Resolves the card only on first use (the DC may start later) and retries if discovery failed.
    private Task<AIAgent> Agent(CancellationToken ct)
    {
        if (_agent is null || _agent.IsFaulted || _agent.IsCanceled)
            _agent = new A2ACardResolver(new Uri(cfg["DistributionCenter:Url"] ?? "http://localhost:5180"), Http)
                .GetAIAgentAsync(Http, cancellationToken: ct);
        return _agent;
    }

    [Description("""
        Pede reposição ao agente do CD Sudeste (outro sistema, via protocolo A2A). Escreva o pedido em português:
        loja (ID e nome), categoria, quantidade de caixas e urgência. Devolve a proposta do CD: número, entregas
        por janela de caminhão e validade. A proposta não é pedido: o pedido é CreateEmergencyOrder, com aprovação.
        """)]
    public async Task<string> RequestProposalAsync(
        [Description("O pedido ao CD, em uma ou duas frases.")] string request, CancellationToken ct = default)
    {
        var dc = await Agent(ct);
        var response = await dc.RunAsync(request, cancellationToken: ct);
        return response.Text;
    }
}
