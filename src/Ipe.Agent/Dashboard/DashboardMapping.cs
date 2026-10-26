using System.Globalization;
using System.Text.Json;
using AGUI.Abstractions;
using AGUI.Server;
using Ipe.Agent.Data;
using Ipe.Agent.OnCall;
using Ipe.Agent.Tools;
using Microsoft.Extensions.AI;

namespace Ipe.Agent.Dashboard;

// Server-side tool-based generative UI: when a data tool finishes, its result becomes a STATE_DELTA
// for the dashboard. The model spends no tokens drawing the screen; the dashboard builds itself
// while the agent works. Registered on the endpoint with MapAGUIServer(...).WithMetadata(...).
// Labels and summaries are on-screen text, so they stay in Portuguese.
public static class DashboardMapping
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static AGUIStreamOptions Options() => new AGUIStreamOptions()
        // Progress (moment 2): each data tool call opens a "running" step (MapCall) and the result closes it
        // with a summary (MapResult). Deterministic and token-free: it doesn't depend on the model.
        .MapCall("GetFreezerStatus", c => Start(c, "Analisando freezers"))
        .MapCall("GetWeeklySales", c => Start(c, "Analisando vendas da semana"))
        .MapCall("GetCriticalStock", c => Start(c, "Verificando estoque"))
        .MapCall("GetTemperatureHistory", c => Start(c, $"Histórico de temperatura · {Arg(c, "freezerId")}"))
        .MapCall("ListMaintenanceTickets", c => Start(c, $"Chamados de manutenção · {Arg(c, "freezerId") ?? Arg(c, "storeId")}"))
        .MapCall("SearchKnowledgeBase", c => Start(c, "Consultando a base de conhecimento"))
        .MapCall("PublishAlerts", c => Start(c, "Publicando alertas no painel"))
        .MapResult("GetFreezerStatus", r => Delta(r, (IReadOnlyList<FreezerStatus> f) => [("/kpis/freezers", FreezerKpi(f))],
            f => $"{f.Count} fora da faixa"))
        .MapResult("GetWeeklySales", r => Delta(r, (IReadOnlyList<WeeklySales> s) =>
            [("/kpis/sales", SalesKpi(s)), ("/charts/sales", SalesChart(s))],
            s => $"{s.Count} variação(ões) acima de 15%"))
        .MapResult("GetCriticalStock", r => Delta(r, (IReadOnlyList<CriticalStockItem> s) => [("/kpis/stock", StockKpi(s))],
            s => $"{s.Count} itens abaixo do ponto"))
        .MapResult("GetTemperatureHistory", r => Delta(r, (TemperatureHistory? h) =>
            h is null ? [] : [($"/charts/temp-{h.FreezerId}", TemperatureChart(h))],
            h => h is null ? "freezer não encontrado" : $"{h.Points.Count} pontos"))
        .MapResult("ListMaintenanceTickets", r => Delta(r, (IReadOnlyList<MaintenanceTicket> t) => [], t => $"{t.Count} chamado(s)"))
        .MapResult("SearchKnowledgeBase", r => Delta(r, (IReadOnlyList<Knowledge.Passage> p) => [],
            p => p.Count == 0 ? "nada encontrado" : string.Join(", ", p.Select(x => x.Document).Distinct())))
        .MapResult("PublishAlerts", r => Delta(r, (IReadOnlyList<Alert> a) => [("/alerts", a)], a => $"{a.Count} alerta(s)"))
        // An approved and executed action goes onto the dashboard. A rejected one has no ActionConfirmation: nothing shows.
        // Actions don't open a step (no MapCall): the delta can't close a step that doesn't exist.
        .MapResult("OpenMaintenanceTicket", r => Delta(r, (ActionConfirmation c) => [("/actions/-", c)], hasStep: false))
        .MapResult("CreateEmergencyOrder", r => Delta(r, (ActionConfirmation c) => [("/actions/-", c)], hasStep: false))
        // On-call: the approved plan becomes the two actions in the "Ações executadas" card.
        .MapResult("ExecuteContingencyPlan", r =>
            Delta(r, (ContingencyConfirmation c) => [("/actions/-", c.Ticket), ("/actions/-", c.Order)], hasStep: false));

    public static Kpi FreezerKpi(IReadOnlyList<FreezerStatus> notOk)
    {
        var critical = notOk.Count(f => f.Status == "CRITICO");
        var warning = notOk.Count(f => f.Status == "ATENCAO");
        return new Kpi("Freezers fora da faixa", notOk.Count.ToString(PtBr),
            notOk.Count == 0 ? "todos OK" : $"{critical} crítico · {warning} em atenção",
            null, critical > 0 ? "critical" : warning > 0 ? "warning" : "ok");
    }

    public static Kpi SalesKpi(IReadOnlyList<WeeklySales> outliers)
    {
        var biggest = outliers.Where(s => s.ChangePct is not null).MaxBy(s => Math.Abs(s.ChangePct!.Value));
        if (biggest is null) return new Kpi("Vendas fora do padrão", "0", "nenhuma variação acima de 15%", "flat", "ok");
        return new Kpi("Maior variação de vendas", Percent(biggest.ChangePct!.Value), $"{biggest.Category} · {biggest.Store}",
            biggest.ChangePct > 0 ? "up" : "down", "warning");
    }

    public static Kpi StockKpi(IReadOnlyList<CriticalStockItem> items)
    {
        var stores = items.Select(i => i.Store).Distinct().ToList();
        var stockout = items.Any(i => i.Quantity < i.ReorderPoint * 0.25m); // policy: < 25% of the reorder point
        return new Kpi("Itens abaixo do ponto", items.Count.ToString(PtBr),
            stores.Count == 0 ? "estoque em dia" : $"em {stores.Count} loja(s): {string.Join(", ", stores)}",
            null, stockout ? "critical" : items.Count > 0 ? "warning" : "ok");
    }

    public static Chart SalesChart(IReadOnlyList<WeeklySales> outliers) => new(
        "sales", "bar", "Vendas: variação semanal acima de 15%",
        [new ChartSeries("Variação %", outliers
            .Where(s => s.ChangePct is not null)
            .OrderByDescending(s => s.ChangePct)
            .Select(s => new Point($"{s.Store} · {s.Category}", s.ChangePct!.Value))
            .ToList())]);

    public static Chart TemperatureChart(TemperatureHistory h) => new(
        $"temp-{h.FreezerId}", "line", $"Temperatura · {h.FreezerId}",
        [
            new ChartSeries("Média", h.Points.Select(p => new Point(p.Start.ToString("s", CultureInfo.InvariantCulture), p.Avg)).ToList()),
            new ChartSeries("Máxima", h.Points.Select(p => new Point(p.Start.ToString("s", CultureInfo.InvariantCulture), p.Max)).ToList()),
        ],
        new Band(h.MinTempC, h.MaxTempC),
        Threshold: -15m, // manual: above -15 °C for 2 h = critical
        StoreId: h.StoreId,
        Region: h.Region);

    private static string Percent(decimal pct) => (pct > 0 ? "+" : "") + pct.ToString("0.0", PtBr) + "%";

    private static string? Arg(FunctionCallContent c, string name) =>
        c.Arguments?.TryGetValue(name, out var v) is true && v is not null ? v.ToString() : null;

    private static IEnumerable<BaseEvent> Start(FunctionCallContent call, string label) =>
        [Event([("/progress/" + call.CallId, new Step(label, "running", null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))])];

    // Reads the tool result (JsonElement serialized by AIFunctionFactory) and emits a STATE_DELTA:
    // closes the call's progress step and applies one "add" per dashboard path. An unreadable result
    // (error, rejection) closes the step and leaves the rest of the dashboard alone.
    private static IEnumerable<BaseEvent> Delta<T>(
        FunctionResultContent result,
        Func<T, IEnumerable<(string Path, object? Value)>> map,
        Func<T, string?>? summary = null,
        bool hasStep = true)
    {
        T? value;
        try
        {
            value = result.Result switch
            {
                JsonElement e => e.Deserialize<T>(Json),
                string s => JsonSerializer.Deserialize<T>(s, Json),
                T t => t,
                _ => default,
            };
        }
        catch (JsonException)
        {
            value = default;
        }

        var step = "/progress/" + result.CallId;
        var operations = new List<(string, object?)>();
        if (hasStep) operations.Add((step + "/status", "done"));
        if (hasStep && value is not null && summary?.Invoke(value) is { } text) operations.Add((step + "/detail", text));
        if (value is not null) operations.AddRange(map(value));
        if (operations.Count > 0) yield return Event(operations);
    }

    private static StateDeltaEvent Event(IEnumerable<(string Path, object? Value)> operations) => new()
    {
        Delta = JsonSerializer.SerializeToElement(operations.Select(o => new { op = "add", path = o.Path, value = o.Value }), Json),
    };
}
