namespace Ipe.Agent.Data;

// Small, typed objects the tools return to the model (never table dumps).

public sealed record FreezerStatus(
    string FreezerId,
    int StoreId,
    string Store,
    string City,
    string Region,
    string Model,
    decimal? Avg24h,
    decimal? Max24h,
    int? HoursAboveMinus15,
    int? Readings24h,
    decimal? AvgTwoWeeksAgo,
    int? LongestGap7dHours,
    string Status);

public sealed record TemperaturePoint(DateTime Start, decimal Avg, decimal Max);

public sealed record TemperatureHistory(
    string FreezerId,
    int StoreId,
    string Region,
    int HoursPerPoint,
    decimal MinTempC,
    decimal MaxTempC,
    IReadOnlyList<TemperaturePoint> Points);

public sealed record WeeklySales(
    int StoreId,
    string Store,
    string City,
    string Region,
    string Category,
    decimal RevenueThisWeek,
    decimal RevenueLastWeek,
    decimal? ChangePct);

public sealed record CriticalStockItem(
    int StoreId,
    string Store,
    string City,
    string Region,
    string Sku,
    string Product,
    string Category,
    int Quantity,
    int ReorderPoint,
    DateOnly LastRestock);

public sealed record MaintenanceTicket(
    int TicketId,
    string FreezerId,
    int StoreId,
    string Type,
    string Status,
    DateOnly OpenedOn,
    DateOnly? ClosedOn,
    string Description);

public static class ActionType
{
    public const string Ticket = "TICKET";
    public const string EmergencyOrder = "EMERGENCY_ORDER";
}

public sealed record NewAction(
    string Type,
    int StoreId,
    string? Reference,
    string? Priority,
    string Description,
    string? ApprovedBy);

public sealed record ExecutedAction(
    int ActionId,
    string Type,
    int StoreId,
    string? Reference,
    string? Priority,
    string Description,
    string? ApprovedBy,
    DateTime CreatedAt);
