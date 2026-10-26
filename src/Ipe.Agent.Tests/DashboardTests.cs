using Ipe.Agent.Dashboard;
using Ipe.Agent.Tools;

namespace Ipe.Agent.Tests;

// What the dashboard shows for the golden data (Local mode).
public class DashboardTests
{
    private static readonly int[] None = [];

    [Fact]
    public async Task Freezer_kpi_shows_1_critical_and_1_warning()
    {
        var notOk = (await Sources.FreshLocal().GetFreezerStatusAsync(null, None, TestContext.Current.CancellationToken))
            .Where(f => f.Status != "OK").ToList();

        var kpi = DashboardMapping.FreezerKpi(notOk);

        Assert.Equal(("2", "1 crítico · 1 em atenção", "critical"), (kpi.Value, kpi.Detail, kpi.Severity));
    }

    [Fact]
    public async Task Sales_kpi_highlights_beverages_in_Curitiba()
    {
        var sales = (await Sources.FreshLocal().GetWeeklySalesAsync(null, None, TestContext.Current.CancellationToken))
            .Where(s => s.ChangePct is { } p && Math.Abs(p) > 15).ToList();

        var kpi = DashboardMapping.SalesKpi(sales);
        var chart = DashboardMapping.SalesChart(sales);

        Assert.Equal(("+68,9%", "Bebidas · Ipê Batel", "up"), (kpi.Value, kpi.Detail, kpi.Trend));
        Assert.Equal(["Ipê Batel · Bebidas", "Ipê Boa Viagem · Congelados"], chart.Series[0].Points.Select(p => p.X));
    }

    [Fact]
    public async Task Stock_kpi_points_to_stockout_in_2_stores()
    {
        var items = await Sources.FreshLocal().GetCriticalStockAsync(null, None, TestContext.Current.CancellationToken);

        var kpi = DashboardMapping.StockKpi(items);

        Assert.Equal("critical", kpi.Severity);
        Assert.StartsWith("em 2 loja(s)", kpi.Detail);
    }

    [Fact]
    public async Task Temperature_chart_has_ideal_band_and_minus_15_line()
    {
        var history = await Sources.FreshLocal().GetTemperatureHistoryAsync("FRZ-07-2", 14, 6, TestContext.Current.CancellationToken);

        var chart = DashboardMapping.TemperatureChart(history!);

        Assert.Equal(("temp-FRZ-07-2", "line", -15m, 7, "Sudeste"), (chart.Id, chart.Type, chart.Threshold, chart.StoreId, chart.Region));
        Assert.Equal(new Band(-22m, -18m), chart.Band);
        Assert.Equal(["Média", "Máxima"], chart.Series.Select(s => s.Name));
    }

    [Fact]
    public async Task Publish_alerts_creates_stable_ids_and_normalizes_severity()
    {
        var alerts = await new DashboardTools(Sources.FreshLocal()).PublishAlertsAsync(
        [
            new AlertInput(7, "Ipê Cambuí (Campinas)", "Freezer FRZ-07-2 crítico", "CRITICO", "…", ["ata-comite-operacoes-2026-08-14.md"]),
            new AlertInput(4, "Ipê Batel (Curitiba)", "Ruptura de bebidas", "alta", "…", ["GetWeeklySales", "politica-reposicao.md"]),
        ], TestContext.Current.CancellationToken);

        Assert.Equal("ipe-cambui-campinas-freezer-frz-07-2-critico", alerts[0].Id);
        Assert.Equal(["critical", "warning"], alerts.Select(a => a.Severity));
        Assert.Equal(["politica-reposicao.md"], alerts[1].Citations); // a tool name is not a citation
        Assert.Equal(["Sudeste", "Sul"], alerts.Select(a => a.Region)); // the region comes from the database, not the model
    }
}
