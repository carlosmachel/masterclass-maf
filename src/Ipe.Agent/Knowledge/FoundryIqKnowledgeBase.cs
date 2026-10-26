using System.Text.Json;
using Azure.Core;
using Azure.Search.Documents.KnowledgeBases;
using Azure.Search.Documents.KnowledgeBases.Models;

namespace Ipe.Agent.Knowledge;

// Foundry IQ: an Azure AI Search knowledge base over the documents in Blob (created by src/Ipe.FoundryIQ.Setup).
// MAF .NET has no Foundry IQ provider; retrieve is called directly, behind IKnowledgeBase,
// and the citation comes from the references (blob URL → file name).
public sealed class FoundryIqKnowledgeBase(Uri searchEndpoint, string knowledgeBase, TokenCredential credential)
    : IKnowledgeBase
{
    private const int MaxPassages = 3;
    private const int MaxChars = 1500;

    private readonly KnowledgeBaseRetrievalClient _client = new(searchEndpoint, knowledgeBase, credential);

    public async Task<IReadOnlyList<Passage>> SearchAsync(string question, CancellationToken ct = default)
    {
        var request = new KnowledgeBaseRetrievalRequest();
        request.Intents.Add(new KnowledgeRetrievalSemanticIntent(question));
        var response = (await _client.RetrieveAsync(request, cancellationToken: ct)).Value;

        // Extractive output: a JSON [{ ref_id, content }] whose ref_id points to References[].Id.
        var documents = response.References
            .OfType<KnowledgeBaseAzureBlobReference>()
            .ToDictionary(r => r.Id, r => Uri.UnescapeDataString(Path.GetFileName(r.BlobUrl.AbsolutePath)));
        var text = (response.Response.FirstOrDefault()?.Content.FirstOrDefault() as KnowledgeBaseMessageTextContent)?.Text;
        if (string.IsNullOrWhiteSpace(text)) return [];

        var passages = new List<Passage>();
        foreach (var item in JsonDocument.Parse(text).RootElement.EnumerateArray())
        {
            var refId = item.GetProperty("ref_id").ToString();
            var content = item.GetProperty("content").GetString() ?? "";
            var document = documents.GetValueOrDefault(refId, $"referência {refId}");
            var title = content.Split('\n').FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? document;
            passages.Add(new Passage(document, title, null,
                content.Length > MaxChars ? content[..MaxChars] + "…" : content));
            if (passages.Count == MaxPassages) break;
        }
        return passages;
    }
}
