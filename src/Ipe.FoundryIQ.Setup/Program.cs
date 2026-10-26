// creates the Foundry IQ knowledge base (Azure AI Search) from the documents in Blob.
// Everything with Entra ID (DefaultAzureCredential): no Search, Storage or OpenAI keys.
//
// Usage:
//   FIQ__Search=https://<search>.search.windows.net FIQ__StorageId=/subscriptions/.../storageAccounts/<account> \
//   FIQ__AiEndpoint=https://<resource>.openai.azure.com dotnet run --project src/Ipe.FoundryIQ.Setup
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.KnowledgeBases;
using Azure.Search.Documents.KnowledgeBases.Models;

string Cfg(string name) => Environment.GetEnvironmentVariable($"FIQ__{name}")
    ?? throw new InvalidOperationException($"Defina FIQ__{name}.");

var search = new Uri(Cfg("Search"));
var credential = new DefaultAzureCredential();
var indexes = new SearchIndexClient(search, credential);
const string Source = "ipe-docs";
const string KnowledgeBaseName = "ipe-conhecimento";

// 1. Blob knowledge source: the service generates the data source, skillset (chunking + embeddings), index and indexer.
//    The connection is by ResourceId: Search reads Storage with its own managed identity.
var model = (string deployment) => new AzureOpenAIVectorizerParameters
{
    ResourceUri = new Uri(Cfg("AiEndpoint")),
    DeploymentName = deployment,
    ModelName = deployment,
};
var source = new AzureBlobKnowledgeSource(Source, new AzureBlobKnowledgeSourceParameters($"ResourceId={Cfg("StorageId")};", "docs")
{
    IngestionParameters = new KnowledgeSourceIngestionParameters
    {
        ContentExtractionMode = KnowledgeSourceContentExtractionMode.Minimal, // .md is text: no Content Understanding
        DisableImageVerbalization = true, // no images in the documents: the ingestion chat model is not used
        EmbeddingModel = new KnowledgeSourceAzureOpenAIVectorizer { AzureOpenAIParameters = model("text-embedding-3-small") },
    },
})
{
    Description = "Documentos da Rede Ipê: ata do Comitê, manual de refrigeração, política de reposição, calendário de promoções.",
};
await indexes.CreateOrUpdateKnowledgeSourceAsync(source);
Console.WriteLine($"Knowledge source '{Source}' criada. Aguardando a ingestão…");

// 2. Waits for the indexer to finish the first sync.
for (var attempt = 0; attempt < 60; attempt++)
{
    var status = (await indexes.GetKnowledgeSourceStatusAsync(Source)).Value;
    var last = status.LastSynchronizationState;
    if (last?.EndTime is not null)
    {
        Console.WriteLine($"Ingestão: {last.ItemsUpdatesProcessed} processados, {last.ItemsUpdatesFailed} com falha.");
        if (last.ItemsUpdatesFailed > 0) Environment.Exit(1);
        break;
    }
    await Task.Delay(TimeSpan.FromSeconds(10));
}

// 3. Knowledge base: what the agent queries (retrieve). In the GA SDK the effort is "minimal":
//    no LLM query planning, hybrid search with semantic reranking.
await indexes.CreateOrUpdateKnowledgeBaseAsync(new KnowledgeBase(KnowledgeBaseName, [new KnowledgeSourceReference(Source)])
{
    Description = "Base de conhecimento operacional da Rede Ipê.",
});
Console.WriteLine($"Knowledge base '{KnowledgeBaseName}' criada.");

// 4. Test retrieve.
var kb = new KnowledgeBaseRetrievalClient(search, KnowledgeBaseName, credential);
var request = new KnowledgeBaseRetrievalRequest();
request.Intents.Add(new KnowledgeRetrievalSemanticIntent("Por que a troca do compressor do freezer de Campinas foi adiada?"));
var response = (await kb.RetrieveAsync(request)).Value;
Console.WriteLine($"Referências: {string.Join(", ", response.References.OfType<KnowledgeBaseAzureBlobReference>().Select(r => r.BlobUrl))}");
var text = (response.Response.FirstOrDefault()?.Content.FirstOrDefault() as KnowledgeBaseMessageTextContent)?.Text ?? "";
Console.WriteLine(text.Length > 600 ? text[..600] + "…" : text);
