using Ipe.Agent.Data;

namespace Ipe.Agent.Tests;

// The 3 stories from data/GABARITO.md, checked in both modes.
public class GoldenDataTests
{
    private static readonly int[] None = [];

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Campinas_FRZ_07_2_is_critical(Mode mode)
    {
        var status = await Sources.Repository(mode).GetFreezerStatusAsync(null, None, TestContext.Current.CancellationToken);

        var frz = Assert.Single(status, f => f.FreezerId == "FRZ-07-2");
        Assert.Equal("CRITICO", frz.Status);
        Assert.Equal("Campinas", frz.City);
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Recife_FRZ_13_1_needs_attention_for_a_gap_longer_than_6h(Mode mode)
    {
        var status = await Sources.Repository(mode).GetFreezerStatusAsync(null, None, TestContext.Current.CancellationToken);

        var frz = Assert.Single(status, f => f.FreezerId == "FRZ-13-1");
        Assert.Equal("ATENCAO", frz.Status);
        Assert.True(frz.LongestGap7dHours > 6, $"gap = {frz.LongestGap7dHours}h");
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Only_the_planted_stories_have_a_freezer_out_of_OK(Mode mode)
    {
        var status = await Sources.Repository(mode).GetFreezerStatusAsync(null, None, TestContext.Current.CancellationToken);

        var notOk = status.Where(f => f.Status != "OK").Select(f => f.FreezerId).Order().ToArray();
        Assert.Equal(["FRZ-07-2", "FRZ-13-1"], notOk);
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Curitiba_beverages_above_50_percent(Mode mode)
    {
        var sales = await Sources.Repository(mode).GetWeeklySalesAsync(null, None, TestContext.Current.CancellationToken);

        var beverages = Assert.Single(sales, s => s.StoreId == 4 && s.Category == "Bebidas");
        Assert.True(beverages.ChangePct > 50, $"change = {beverages.ChangePct}%");
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Recife_frozen_food_dropping(Mode mode)
    {
        var sales = await Sources.Repository(mode).GetWeeklySalesAsync(null, None, TestContext.Current.CancellationToken);

        var frozen = Assert.Single(sales, s => s.StoreId == 13 && s.Category == "Congelados");
        Assert.True(frozen.ChangePct < 0, $"change = {frozen.ChangePct}%");
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Critical_stock_only_in_stores_4_and_13(Mode mode)
    {
        var items = await Sources.Repository(mode).GetCriticalStockAsync(null, None, TestContext.Current.CancellationToken);

        Assert.Equal([4, 13], items.Select(i => i.StoreId).Distinct().Order().ToArray());
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Excluding_store_4_removes_Curitiba_from_sales_and_stock(Mode mode)
    {
        var repo = Sources.Repository(mode);
        var ct = TestContext.Current.CancellationToken;

        var sales = await repo.GetWeeklySalesAsync(null, [4], ct);
        var stock = await repo.GetCriticalStockAsync(null, [4], ct);

        Assert.DoesNotContain(sales, s => s.StoreId == 4);
        Assert.Equal([13], stock.Select(i => i.StoreId).Distinct().ToArray());
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Campinas_history_shows_the_rise(Mode mode)
    {
        var history = await Sources.Repository(mode).GetTemperatureHistoryAsync("FRZ-07-2", 14, 6, TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.Equal(-22.0m, history.MinTempC);
        Assert.Equal(-18.0m, history.MaxTempC);
        Assert.InRange(history.Points.Count, 50, 57); // 14 days in 6h blocks
        var start = history.Points.Take(8).Average(p => p.Avg);
        var end = history.Points.TakeLast(8).Average(p => p.Avg);
        Assert.True(end - start > 4, $"start {start:F1} °C, end {end:F1} °C");
        Assert.Contains(history.Points, p => p.Max > -15);
    }

    [Theory]
    [InlineData(Mode.Local)]
    [InlineData(Mode.Azure)]
    public async Task Campinas_stopgap_ticket_and_Recife_open_ticket(Mode mode)
    {
        var repo = Sources.Repository(mode);
        var ct = TestContext.Current.CancellationToken;

        var campinas = await repo.ListMaintenanceTicketsAsync("FRZ-07-2", null, ct);
        var recife = await repo.ListMaintenanceTicketsAsync("FRZ-13-1", null, ct);

        Assert.Contains(campinas, t => t.Description.Contains("paliativo"));
        Assert.Contains(recife, t => t.Status == "Aberto");
    }
}
