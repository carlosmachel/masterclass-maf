using Microsoft.EntityFrameworkCore;
using Db = Ipe.Agent.Data.Entities;

namespace Ipe.Agent.Data;

// Azure SQL through EF Core. The connection string uses "Authentication=Active Directory Default"
// (same chain as DefaultAzureCredential). The status rules live in the views created by the migration.
public sealed class SqlOperationsRepository(IDbContextFactory<IpeDbContext> contexts) : IOperationsRepository
{
    // Origin for the hour buckets of the temperature chart (same as DATEDIFF(HOUR, 0, ...) in T-SQL).
    private static readonly DateTime s_epoch = new(1900, 1, 1);

    public async Task<IReadOnlyList<FreezerStatus>> GetFreezerStatusAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.FreezerStatus
            .Where(s => region == null || s.Store.Region == region)
            .Where(s => !excludedStoreIds.Contains(s.StoreId))
            .OrderBy(s => s.FreezerId)
            .Select(s => new FreezerStatus(s.FreezerId, s.StoreId, s.StoreName, s.City, s.Store.Region, s.Model,
                s.Avg24h, s.Max24h, s.HoursAboveMinus15, s.Readings24h, s.AvgTwoWeeksAgo, s.LongestGap7dHours, s.Status))
            .ToListAsync(ct);
    }

    public async Task<TemperatureHistory?> GetTemperatureHistoryAsync(
        string freezerId, int days, int hoursPerPoint, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var freezer = await db.Freezers
            .Where(f => f.FreezerId == freezerId)
            .Select(f => new { f.TempMinC, f.TempMaxC, f.StoreId, f.Store.Region })
            .SingleOrDefaultAsync(ct);
        if (freezer is null) return null;

        // "Now" is the most recent reading in the database, as in the views.
        var nowAt = await db.FreezerReadings.MaxAsync(r => (DateTime?)r.ReadAt, ct) ?? DateTime.UtcNow;
        var from = nowAt.AddDays(-days);

        var buckets = await db.FreezerReadings
            .Where(r => r.FreezerId == freezerId && r.ReadAt > from)
            .GroupBy(r => EF.Functions.DateDiffHour(s_epoch, r.ReadAt) / hoursPerPoint)
            .Select(g => new { Bucket = g.Key, Avg = g.Average(r => r.TemperatureC), Max = g.Max(r => r.TemperatureC) })
            .OrderBy(b => b.Bucket)
            .ToListAsync(ct);

        var points = buckets
            .Select(b => new TemperaturePoint(
                s_epoch.AddHours(b.Bucket * hoursPerPoint), Math.Round(b.Avg, 2, MidpointRounding.AwayFromZero), b.Max))
            .ToList();
        return new TemperatureHistory(freezerId, freezer.StoreId, freezer.Region, hoursPerPoint, freezer.TempMinC, freezer.TempMaxC, points);
    }

    public async Task<IReadOnlyList<WeeklySales>> GetWeeklySalesAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.WeeklySales
            .Where(v => region == null || v.Store.Region == region)
            .Where(v => !excludedStoreIds.Contains(v.StoreId))
            .OrderBy(v => v.StoreId).ThenBy(v => v.Category)
            .Select(v => new WeeklySales(v.StoreId, v.StoreName, v.City, v.Store.Region, v.Category,
                v.RevenueWeek, v.RevenuePrevWeek, v.ChangePct))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CriticalStockItem>> GetCriticalStockAsync(
        int? storeId, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.CriticalStock
            .Where(c => storeId == null || c.StoreId == storeId)
            .Where(c => !excludedStoreIds.Contains(c.StoreId))
            .OrderBy(c => c.StoreId).ThenBy(c => c.Sku)
            .Select(c => new CriticalStockItem(c.StoreId, c.StoreName, c.City, c.Store.Region, c.Sku,
                c.Product, c.Category, c.Quantity, c.ReorderPoint, c.LastRestock))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> ListMaintenanceTicketsAsync(
        string? freezerId, int? storeId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.MaintenanceTickets
            .Where(t => (freezerId == null || t.FreezerId == freezerId) && (storeId == null || t.StoreId == storeId))
            .OrderByDescending(t => t.OpenedOn)
            .Select(t => new MaintenanceTicket(t.TicketId, t.FreezerId, t.StoreId, t.Type, t.Status,
                t.OpenedOn, t.ClosedOn, t.Description))
            .ToListAsync(ct);
    }

    public async Task<int?> GetFreezerStoreAsync(string freezerId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Freezers.Where(f => f.FreezerId == freezerId).Select(f => (int?)f.StoreId).SingleOrDefaultAsync(ct);
    }

    public async Task<string?> GetStoreRegionAsync(int storeId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Stores.Where(s => s.StoreId == storeId).Select(s => s.Region).SingleOrDefaultAsync(ct);
    }

    public async Task<ExecutedAction> RecordActionAsync(NewAction action, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = new Db.ExecutedActionRow
        {
            Type = action.Type,
            StoreId = action.StoreId,
            Reference = action.Reference,
            Priority = action.Priority,
            Description = action.Description,
            ApprovedBy = action.ApprovedBy,
        };
        db.ExecutedActions.Add(row);
        await db.SaveChangesAsync(ct);   // fills ActionId and CreatedAt (database default) from the INSERT
        return ToModel(row);
    }

    public async Task<IReadOnlyList<ExecutedAction>> ListActionsAsync(CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.ExecutedActions.AsNoTracking().OrderBy(a => a.ActionId).ToListAsync(ct);
        return rows.Select(ToModel).ToList();
    }

    private static ExecutedAction ToModel(Db.ExecutedActionRow a) =>
        new(a.ActionId, a.Type, a.StoreId, a.Reference, a.Priority, a.Description, a.ApprovedBy, a.CreatedAt);
}
