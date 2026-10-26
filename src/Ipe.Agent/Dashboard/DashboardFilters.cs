using System.Text.Json;
using AGUI.Server;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Dashboard;

public sealed record Filters(string? Region, IReadOnlyCollection<int> ExcludedStoreIds)
{
    public static readonly Filters None = new(null, []);
    public bool IsEmpty => Region is null && ExcludedStoreIds.Count == 0;
}

// Filters the user picked on the dashboard, read from the AG-UI state (RunAgentInput.State.filters).
// The state comes from the client: only a known region and integer ids get through.
public static class DashboardFilters
{
    public static readonly string[] Regions = ["Sudeste", "Sul", "Nordeste", "Centro-Oeste", "Norte"];

    // Inside a tool: the request's ChatOptions are in the FunctionInvokingChatClient context.
    public static Filters Current() => Read(FunctionInvokingChatClient.CurrentContext?.Options);

    public static Filters Read(ChatOptions? options)
    {
        if (options is null
            || !options.TryGetRunAgentInput(out var input)
            || input.State is not { ValueKind: JsonValueKind.Object } state
            || !state.TryGetProperty("filters", out var filters)
            || filters.ValueKind != JsonValueKind.Object)
        {
            return Filters.None;
        }

        string? region = null;
        if (filters.TryGetProperty("region", out var r) && r.ValueKind == JsonValueKind.String)
            region = Regions.FirstOrDefault(x => string.Equals(x, r.GetString(), StringComparison.OrdinalIgnoreCase));

        var excluded = new List<int>();
        if (filters.TryGetProperty("excludedStoreIds", out var s) && s.ValueKind == JsonValueKind.Array)
            foreach (var item in s.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var id) && id > 0) excluded.Add(id);

        return new Filters(region, excluded.Distinct().ToList());
    }

    // The dashboard filter always applies; whatever the model passes adds to it.
    public static (string? Region, IReadOnlyCollection<int> Excluded) Combine(string? regionFromModel, int[]? excludedFromModel)
    {
        var dashboard = Current();
        return (dashboard.Region ?? regionFromModel,
                (excludedFromModel ?? []).Union(dashboard.ExcludedStoreIds).ToList());
    }
}
