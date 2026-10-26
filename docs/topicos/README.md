# Tópicos técnicos da masterclass

Cada cena do deck tem um tópico técnico, e cada tópico tem um documento aqui. Os documentos explicam o conceito, mostram onde ele está no código da Rede Ipê e apontam para a documentação oficial. Servem para estudar e para consultar depois da sessão.

| Cena | Tópico | Documento |
| --- | --- | --- |
| 0 | A arquitetura da demo | [00-arquitetura.md](00-arquitetura.md) |
| 1 | Criar um agente Harness | [01-agente-harness.md](01-agente-harness.md) |
| 2 | RAG com Foundry IQ como tool | [02-rag-foundry-iq.md](02-rag-foundry-iq.md) |
| 3 | Estado compartilhado com AG-UI | [03-estado-compartilhado.md](03-estado-compartilhado.md) |
| 4 | Aprovação humana de tools | [04-aprovacao-humana.md](04-aprovacao-humana.md) |
| 5 | Middleware contra prompt injection | [05-prompt-injection.md](05-prompt-injection.md) |
| 6 | Workflows e A2A | [06-workflows-a2a.md](06-workflows-a2a.md) |
| 7 | Eval de agente | [07-eval.md](07-eval.md) |
| 7b | Agente como servidor MCP | [07b-mcp.md](07b-mcp.md) |

Versões usadas na demo (`Directory.Packages.props`): `Microsoft.Agents.AI` e `Microsoft.Agents.AI.Harness` 1.24.0, os pacotes de hosting (AG-UI, A2A) em `1.24.0-preview.261006.1`, `ModelContextProtocol.AspNetCore` 2.2.0, `Azure.Search.Documents` 12.0.0 e `Microsoft.Extensions.AI.Evaluation` 10.10.0. Os pacotes em preview ainda mudam de API entre versões.
