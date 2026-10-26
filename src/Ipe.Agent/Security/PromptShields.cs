using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;

namespace Ipe.Agent.Security;

// Prompt Shields (Azure AI Content Safety) with an Entra token, no key. The endpoint is the same AI Services
// resource as the model. There is no stable .NET SDK for shieldPrompt; the REST call is one line.
public sealed class PromptShields(HttpClient http, Uri endpoint, TokenCredential credential)
{
    private const string ApiVersion = "2024-09-01";
    private const int MaxDocumentsPerCall = 5;   // API limit
    private static readonly TokenRequestContext Scope = new(["https://cognitiveservices.azure.com/.default"]);

    // For each document, true when the service detected an attack (an instruction hidden for the model).
    public async Task<bool[]> DetectDocumentAttacksAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(Scope, ct);
        var attacks = new List<bool>(documents.Count);
        foreach (var batch in documents.Chunk(MaxDocumentsPerCall))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(endpoint, $"contentsafety/text:shieldPrompt?api-version={ApiVersion}"))
            {
                Content = JsonContent.Create(new ShieldRequest(userPrompt, batch)),
            };
            request.Headers.Authorization = new("Bearer", token.Token);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var analysis = await response.Content.ReadFromJsonAsync<ShieldResponse>(ct);
            attacks.AddRange(analysis!.DocumentsAnalysis.Select(d => d.AttackDetected));
        }
        return [.. attacks];
    }

    private sealed record ShieldRequest(
        [property: JsonPropertyName("userPrompt")] string UserPrompt,
        [property: JsonPropertyName("documents")] string[] Documents);

    private sealed record ShieldResponse(
        [property: JsonPropertyName("documentsAnalysis")] DocumentAnalysis[] DocumentsAnalysis);

    private sealed record DocumentAnalysis([property: JsonPropertyName("attackDetected")] bool AttackDetected);
}
