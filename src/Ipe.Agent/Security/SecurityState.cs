using System.Text.Json;
using AGUI.Server;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Security;

// Dashboard switch, read from the AG-UI state (RunAgentInput.State.security.promptShields).
public static class SecurityState
{
    public static bool? PromptShields(ChatOptions? options) =>
        options is not null
        && options.TryGetRunAgentInput(out var input)
        && input.State is { ValueKind: JsonValueKind.Object } state
        && state.TryGetProperty("security", out var security)
        && security.ValueKind == JsonValueKind.Object
        && security.TryGetProperty("promptShields", out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
