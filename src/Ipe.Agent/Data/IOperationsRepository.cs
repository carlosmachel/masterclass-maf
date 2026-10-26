namespace Ipe.Agent.Data;

// Source of the operational data. "Now" is always the most recent data in the database, as in the views.
// Implementations: SqlOperationsRepository (Azure SQL via EF Core) and LocalOperationsRepository (data/csv, plan B).
public interface IOperationsRepository
{
    Task<IReadOnlyList<FreezerStatus>> GetFreezerStatusAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default);

    Task<TemperatureHistory?> GetTemperatureHistoryAsync(
        string freezerId, int days, int hoursPerPoint, CancellationToken ct = default);

    Task<IReadOnlyList<WeeklySales>> GetWeeklySalesAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default);

    Task<IReadOnlyList<CriticalStockItem>> GetCriticalStockAsync(
        int? storeId, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default);

    Task<IReadOnlyList<MaintenanceTicket>> ListMaintenanceTicketsAsync(
        string? freezerId, int? storeId, CancellationToken ct = default);

    Task<int?> GetFreezerStoreAsync(string freezerId, CancellationToken ct = default);

    Task<string?> GetStoreRegionAsync(int storeId, CancellationToken ct = default);

    Task<ExecutedAction> RecordActionAsync(NewAction action, CancellationToken ct = default);

    Task<IReadOnlyList<ExecutedAction>> ListActionsAsync(CancellationToken ct = default);
}
