using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AGUI.Server;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Ipe.Agent;

// Same agent, same tools, two models: each run picks one from the AG-UI state ("model": "foundry" | "local").
// Foundry = Azure OpenAI (DefaultAzureCredential). Local = Ollama on its OpenAI-compatible API (LocalModel:*).
// MAF only sees an IChatClient; switching providers changes nothing in the agent.
public sealed class ModelSelector(IChatClient foundry, IChatClient? local) : IChatClient
{
    public static IChatClient Create(IConfiguration cfg) => new ModelSelector(Foundry(cfg), Local(cfg));

    // Both clients are public so the eval (src/Ipe.Agent.Evals) can pin the model without AG-UI state.
    public static IChatClient Foundry(IConfiguration cfg) =>
        new AzureOpenAIClient(new Uri(cfg["AzureOpenAI:Endpoint"]!), new DefaultAzureCredential())
            .GetChatClient(cfg["AzureOpenAI:Deployment"]!)
            .AsIChatClient();

    public static IChatClient? Local(IConfiguration cfg) =>
        cfg["LocalModel:Model"] is not { Length: > 0 } model ? null
        : new OpenAIClient(
                new ApiKeyCredential("ollama"),   // Ollama ignores the key; the SDK requires one. Not a secret.
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(cfg["LocalModel:Endpoint"] ?? "http://localhost:11434/v1"),
                    // LocalModel:TimeoutSeconds (default: the SDK's 100 s). The eval raises it: with several
                    // requests queued, Ollama takes longer than 100 s per call and the case ended with no verdict.
                    NetworkTimeout = cfg.GetValue<int?>("LocalModel:TimeoutSeconds") is { } s ? TimeSpan.FromSeconds(s) : null,
                })
            .GetChatClient(model)
            .AsIChatClient()
            .AsBuilder()
            // Local model without reasoning: on Ollama, reasoning_effort "none" turns off qwen3 thinking (3–4x faster).
            .ConfigureOptions(o => o.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None })
            .Build();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => Pick(options).GetResponseAsync(messages, options, cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in Pick(options).GetStreamingResponseAsync(messages, options, cancellationToken))
            yield return update;
    }

    private IChatClient Pick(ChatOptions? options) => local is not null && WantsLocal(options) ? local : foundry;

    private static bool WantsLocal(ChatOptions? options) =>
        options is not null
        && options.TryGetRunAgentInput(out var input)
        && input.State is { ValueKind: JsonValueKind.Object } state
        && state.TryGetProperty("model", out var model)
        && model.ValueKind == JsonValueKind.String
        && model.GetString() == "local";

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : foundry.GetService(serviceType, serviceKey);

    public void Dispose()
    {
        foundry.Dispose();
        local?.Dispose();
    }
}
