namespace Ipe.Agent.Data.Entities;

// EF Core entities (code-first). Table and column names are snake_case (see IpeDbContext);
// the records the tools return live in Models.cs (hence the Row suffix where the names would clash).

public sealed class Store
{
    public int StoreId { get; set; }
    public required string Name { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string Region { get; set; }
    public required string Manager { get; set; }
    public int AreaM2 { get; set; }
    public DateOnly OpenedOn { get; set; }
}

public sealed class Product
{
    public int ProductId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public bool Refrigerated { get; set; }
}

public sealed class DailySale
{
    public DateOnly SaleDate { get; set; }
    public int StoreId { get; set; }
    public required string Category { get; set; }
    public decimal Revenue { get; set; }
    public int Units { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class Freezer
{
    public required string FreezerId { get; set; }
    public int StoreId { get; set; }
    public required string Model { get; set; }
    public DateOnly InstalledOn { get; set; }
    public decimal TempMinC { get; set; }
    public decimal TempMaxC { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class FreezerReading
{
    public required string FreezerId { get; set; }
    public DateTime ReadAt { get; set; }
    public decimal TemperatureC { get; set; }
    public Freezer Freezer { get; set; } = null!;
}

public sealed class StockItem
{
    public int StoreId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int ReorderPoint { get; set; }
    public DateOnly LastRestock { get; set; }
    public Store Store { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

public sealed class MaintenanceTicketRow
{
    public int TicketId { get; set; }
    public required string FreezerId { get; set; }
    public int StoreId { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public DateOnly OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public required string Description { get; set; }
    public Freezer Freezer { get; set; } = null!;
    public Store Store { get; set; } = null!;
}

// What the agent executes after "Aprovar". No foreign key: the action keeps its store id as typed.
public sealed class ExecutedActionRow
{
    public int ActionId { get; set; }
    public required string Type { get; set; }
    public int StoreId { get; set; }
    public string? Reference { get; set; }
    public string? Priority { get; set; }
    public required string Description { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Views (keyless, read-only). They are created by the InitialSchema migration with migrationBuilder.Sql.

public sealed class FreezerStatusView
{
    public required string FreezerId { get; set; }
    public int StoreId { get; set; }
    public required string StoreName { get; set; }
    public required string City { get; set; }
    public required string Model { get; set; }
    public decimal? Avg24h { get; set; }
    public decimal? Max24h { get; set; }
    public int? HoursAboveMinus15 { get; set; }
    public int? Readings24h { get; set; }
    public decimal? AvgTwoWeeksAgo { get; set; }
    public int? LongestGap7dHours { get; set; }
    public required string Status { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class WeeklySalesView
{
    public int StoreId { get; set; }
    public required string StoreName { get; set; }
    public required string City { get; set; }
    public required string Category { get; set; }
    public decimal RevenueWeek { get; set; }
    public decimal RevenuePrevWeek { get; set; }
    public decimal? ChangePct { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class CriticalStockView
{
    public int StoreId { get; set; }
    public required string StoreName { get; set; }
    public required string City { get; set; }
    public required string Sku { get; set; }
    public required string Product { get; set; }
    public required string Category { get; set; }
    public int Quantity { get; set; }
    public int ReorderPoint { get; set; }
    public DateOnly LastRestock { get; set; }
    public Store Store { get; set; } = null!;
}
