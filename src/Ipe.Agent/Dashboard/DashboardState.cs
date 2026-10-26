namespace Ipe.Agent.Dashboard;

// Shape of the dashboard's shared state (AG-UI state), mirrored in web/src/dashboard/types.ts.
// kpis and charts are objects keyed by name so each tool updates only its own part
// with a JSON Patch "add" (which replaces the member if it already exists).
//
// {
//   filters:  { region, excludedStoreIds },
//   kpis:     { freezers?: Kpi, sales?: Kpi, stock?: Kpi },
//   charts:   { [id]: Chart },
//   alerts:   Alert[],
//   actions:  ActionConfirmation[]  (approved and executed actions, in order),
//   progress: { [callId]: Step }  (one step per data tool call),
//   report:   ""
// }

public sealed record Step(string Label, string Status, string? Detail, long Order);

public sealed record Kpi(string Label, string Value, string? Detail, string? Trend, string Severity);

public sealed record Point(string X, decimal Y);

public sealed record ChartSeries(string Name, IReadOnlyList<Point> Points);

public sealed record Band(decimal Min, decimal Max);

public sealed record Chart(
    string Id, string Type, string Title, IReadOnlyList<ChartSeries> Series, Band? Band = null, decimal? Threshold = null,
    int? StoreId = null, string? Region = null);

public sealed record Alert(
    string Id, int StoreId, string Store, string? Region, string Title, string Severity, string Summary,
    IReadOnlyList<string> Citations);
