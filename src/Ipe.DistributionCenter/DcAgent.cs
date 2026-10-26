using A2A;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ipe.DistributionCenter;

// The Distribution Center agent: another "company" (Ipê Logística), another process. It talks to the stores'
// agents over A2A. It answers with proposals; the store confirms the order, with human approval.
public static class DcAgent
{
    public const string Name = "dc-southeast";
    public const string Route = "/a2a/dc-southeast";

    public static AIAgent Create(IConfiguration cfg, IServiceProvider sp)
    {
        var logistics = sp.GetRequiredService<Logistics>();
        return new AzureOpenAIClient(new Uri(cfg["AzureOpenAI:Endpoint"]!), new DefaultAzureCredential())
            .GetChatClient(cfg["AzureOpenAI:Deployment"]!)
            .AsIChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = Name,
                Description = "Agente do CD Sudeste da Ipê Logística: estoque, janelas de caminhão e propostas de reposição.",
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Você é o agente do CD Sudeste (Jundiaí) da Ipê Logística. Quem fala com você são agentes de outros
                        sistemas (lojas, plantão) pedindo reposição. Para cada pedido: CheckCapacity e depois
                        CreateProposal com a quantidade pedida. Responda em até 4 linhas, em português: o número da
                        proposta, as entregas (caixas, horário e veículo), o que não dá para atender e a validade.
                        Você não confirma pedidos: a proposta vira pedido quando a loja cria o pedido emergencial com
                        aprovação humana. Nunca invente números: use só o que as tools devolvem.
                        """,
                    Tools =
                    [
                        AIFunctionFactory.Create(logistics.CheckCapacity, "CheckCapacity"),
                        AIFunctionFactory.Create(logistics.CreateProposal, "CreateProposal"),
                    ],
                    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
                },
            })
            .AsBuilder().UseOpenTelemetry().Build();
    }

    // The Agent Card: what another agent reads at /.well-known/agent-card.json to discover this one.
    public static AgentCard Card(string publicUrl) => new()
    {
        Name = "CD Sudeste · Ipê Logística",
        Description = "Propostas de reposição para as lojas da região Sudeste: estoque do CD e janelas de caminhão.",
        Version = "1.0",
        DefaultInputModes = ["text"],
        DefaultOutputModes = ["text"],
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = publicUrl.TrimEnd('/') + Route,
                ProtocolBinding = ProtocolBindingNames.HttpJson,
                ProtocolVersion = "1.0",
            },
        ],
        Skills =
        [
            new A2A.AgentSkill
            {
                Id = "restock-proposal",
                Name = "Proposta de reposição",
                Description = "Divide o pedido pelas janelas de caminhão e devolve uma proposta com validade.",
                Tags = ["reposição", "congelados", "bebidas", "logística"],
                Examples = ["Preciso de 150 caixas de Congelados para a loja 7, o quanto antes."],
            },
        ],
    };
}
