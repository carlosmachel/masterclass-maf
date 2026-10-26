# Tópico 2 · RAG com Foundry IQ como tool

**Cena 2, o porquê, com citação.** O dado diz o quê (Curitiba vendeu muita bebida, o freezer de Campinas esquentou); o documento diz por quê (o Festival de Bebidas, a ata do Comitê que adiou a troca do compressor). O agente busca nos documentos e cita a fonte.

## O que é o Foundry IQ

Foundry IQ é a camada de conhecimento do Microsoft Foundry, construída sobre o *agentic retrieval* do Azure AI Search. Você cria uma **knowledge base** ligada a uma ou mais **knowledge sources** (aqui, um container de Blob com os `.md` de `data/docs`). Na hora da pergunta, a ação `retrieve` planeja as consultas, roda em paralelo, reordena e devolve os trechos com referência ao documento de origem.

A knowledge base da demo se chama `ipe-conhecimento` e é criada por código em `src/Ipe.FoundryIQ.Setup`. O passo a passo dos recursos Azure está em `data/README.md`.

## No código: conhecimento é mais uma tool

O MAF .NET não tem um provider de Foundry IQ. A demo chama o `retrieve` direto com o SDK do Azure AI Search, atrás de uma interface `IKnowledgeBase`.

`src/Ipe.Agent/Knowledge/FoundryIqKnowledgeBase.cs`:

```csharp
private readonly KnowledgeBaseRetrievalClient _client =
    new(searchEndpoint, knowledgeBase, credential);   // DefaultAzureCredential

public async Task<IReadOnlyList<Passage>> SearchAsync(string question, CancellationToken ct = default)
{
    var request = new KnowledgeBaseRetrievalRequest();
    request.Intents.Add(new KnowledgeRetrievalSemanticIntent(question));
    var response = (await _client.RetrieveAsync(request, cancellationToken: ct)).Value;

    // a citação vem das references: URL do blob → nome do arquivo
    var documents = response.References
        .OfType<KnowledgeBaseAzureBlobReference>()
        .ToDictionary(r => r.Id, r => Path.GetFileName(r.BlobUrl.AbsolutePath));
    ...
}
```

A saída extrativa é um JSON `[{ ref_id, content }]`; o `ref_id` aponta para `References[].Id`, e é daí que sai o nome do documento citado no chip da tela. A demo limita a 3 trechos de até 1.500 caracteres.

No modo Local (`Demo:Mode = Local`), a mesma interface é atendida por `LocalKnowledgeBase`, que lê `data/docs` do disco. O eval usa esse modo.

## Por que como tool, e não como contexto fixo

- O agente decide quando buscar. Uma pergunta só sobre números não paga a busca.
- O resultado passa pelo mesmo pipeline das outras tools: aparece no progresso do painel ("Consultando a base de conhecimento") e passa pelo Prompt Shields antes de chegar ao modelo (tópico 5).

## Armadilhas que já apareceram

- No tier Free do Search, um reindex sob demanda a cada 3 minutos (HTTP 429): espere e tente de novo.
- Modelos pequenos "traduzem" o nome do arquivo citado (`política-de-reposição.md` no lugar de `politica-reposicao.md`). `CitationFixer.cs` aprende os nomes reais nos resultados de `SearchKnowledgeBase` e troca o nome parecido pelo real, no texto e nos argumentos das tools. Nome sem par próximo fica como está, e o golden eval pega.

## Para ir além

- [O que é o Foundry IQ (FAQ)](https://learn.microsoft.com/azure/foundry/agents/concepts/foundry-iq-faq)
- [Agentic retrieval no Azure AI Search](https://learn.microsoft.com/azure/search/agentic-retrieval-overview)
- [Criar uma knowledge base](https://learn.microsoft.com/azure/search/agentic-retrieval-how-to-create-knowledge-base)
- [Consultar a knowledge base com `retrieve` ou MCP](https://learn.microsoft.com/azure/search/agentic-retrieval-how-to-retrieve)
