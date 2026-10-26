using System.Collections.Concurrent;
using System.Globalization;

namespace Ipe.Agent.Data;

// Plan B without a database: loads data/csv into memory and reproduces the views from the InitialSchema migration (Data/Migrations).
// Comparisons with a missing value (NULL in SQL) are false, as in the view's CASE.
public sealed class LocalOperationsRepository : IOperationsRepository
{
    private sealed record Store(int Id, string Name, string City, string Region);
    private sealed record Product(int Id, string Sku, string Name, string Category);
    private sealed record Freezer(string Id, int StoreId, string Model, decimal TempMin, decimal TempMax);
    private sealed record Reading(string FreezerId, DateTime ReadAt, decimal Temperature);
    private sealed record Sale(DateOnly Date, int StoreId, string Category, decimal Revenue);
    private sealed record Stock(int StoreId, int ProductId, int Quantity, int ReorderPoint, DateOnly LastRestock);

    private readonly Dictionary<int, Store> _stores;
    private readonly Dictionary<int, Product> _products;
    private readonly Dictionary<string, Freezer> _freezers;
    private readonly ILookup<string, Reading> _readings;
    private readonly List<Sale> _sales;
    private readonly List<Stock> _stock;
    private readonly List<MaintenanceTicket> _tickets;
    private readonly DateTime _now;
    private readonly DateOnly _today;

    private readonly ConcurrentQueue<ExecutedAction> _actions = new();
    private int _nextAction;

    public LocalOperationsRepository(string csvFolder)
    {
        _stores = Csv.Read(Path.Combine(csvFolder, "stores.csv"))
            .Select(c => new Store(Csv.Int(c["store_id"]), c["name"], c["city"], c["region"]))
            .ToDictionary(s => s.Id);
        _products = Csv.Read(Path.Combine(csvFolder, "products.csv"))
            .Select(c => new Product(Csv.Int(c["product_id"]), c["sku"], c["name"], c["category"]))
            .ToDictionary(p => p.Id);
        _freezers = Csv.Read(Path.Combine(csvFolder, "freezers.csv"))
            .Select(c => new Freezer(c["freezer_id"], Csv.Int(c["store_id"]), c["model"],
                Csv.Dec(c["temp_min_c"]), Csv.Dec(c["temp_max_c"])))
            .ToDictionary(f => f.Id);
        var readings = Csv.Read(Path.Combine(csvFolder, "freezer_readings.csv"))
            .Select(c => new Reading(c["freezer_id"], Csv.DateTime(c["read_at"]), Csv.Dec(c["temperature_c"])))
            .ToList();
        _readings = readings.OrderBy(r => r.ReadAt).ToLookup(r => r.FreezerId);
        _now = readings.Max(r => r.ReadAt);
        _sales = Csv.Read(Path.Combine(csvFolder, "daily_sales.csv"))
            .Select(c => new Sale(Csv.Date(c["sale_date"]), Csv.Int(c["store_id"]), c["category"], Csv.Dec(c["revenue"])))
            .ToList();
        _today = _sales.Max(s => s.Date);
        _stock = Csv.Read(Path.Combine(csvFolder, "stock.csv"))
            .Select(c => new Stock(Csv.Int(c["store_id"]), Csv.Int(c["product_id"]), Csv.Int(c["quantity"]),
                Csv.Int(c["reorder_point"]), Csv.Date(c["last_restock"])))
            .ToList();
        _tickets = Csv.Read(Path.Combine(csvFolder, "maintenance_tickets.csv"))
            .Select(c => new MaintenanceTicket(Csv.Int(c["ticket_id"]), c["freezer_id"], Csv.Int(c["store_id"]), c["type"],
                c["status"], Csv.Date(c["opened_on"]),
                string.IsNullOrEmpty(c["closed_on"]) ? null : Csv.Date(c["closed_on"]), c["description"]))
            .ToList();
    }

    public Task<IReadOnlyList<FreezerStatus>> GetFreezerStatusAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        var result = new List<FreezerStatus>();
        foreach (var f in _freezers.Values.OrderBy(f => f.Id))
        {
            var store = _stores[f.StoreId];
            if (!InFilter(store, region, excludedStoreIds)) continue;

            var all = _readings[f.Id];
            var last24h = all.Where(r => r.ReadAt > _now.AddHours(-24)).ToList();
            var last7d = all.Where(r => r.ReadAt > _now.AddDays(-7)).ToList();
            var twoWeeksAgo = all.Where(r => r.ReadAt <= _now.AddDays(-7) && r.ReadAt > _now.AddDays(-14)).ToList();

            decimal? avg24 = last24h.Count > 0 ? last24h.Average(r => r.Temperature) : null;
            decimal? max24 = last24h.Count > 0 ? last24h.Max(r => r.Temperature) : null;
            int? hoursAbove = last24h.Count > 0 ? last24h.Count(r => r.Temperature > -15) : null;
            int? readings24 = last24h.Count > 0 ? last24h.Count : null;
            decimal? avgTwoWeeksAgo = twoWeeksAgo.Count > 0 ? twoWeeksAgo.Average(r => r.Temperature) : null;
            int? longestGap = last7d.Count > 1
                ? last7d.Zip(last7d.Skip(1), (a, b) => HourDiff(a.ReadAt, b.ReadAt)).Max()
                : null;

            var status = hoursAbove >= 2 ? "CRITICO"
                : avg24 > -18.5m || readings24 < 20 || longestGap > 6 || avg24 - avgTwoWeeksAgo > 2 ? "ATENCAO"
                : "OK";

            result.Add(new FreezerStatus(f.Id, f.StoreId, store.Name, store.City, store.Region, f.Model,
                Round(avg24, 2), max24, hoursAbove, readings24, Round(avgTwoWeeksAgo, 2), longestGap, status));
        }
        return Task.FromResult<IReadOnlyList<FreezerStatus>>(result);
    }

    public Task<TemperatureHistory?> GetTemperatureHistoryAsync(
        string freezerId, int days, int hoursPerPoint, CancellationToken ct = default)
    {
        if (!_freezers.TryGetValue(freezerId, out var f)) return Task.FromResult<TemperatureHistory?>(null);

        var points = _readings[freezerId]
            .Where(r => r.ReadAt > _now.AddDays(-days))
            .GroupBy(r => BlockStart(r.ReadAt, hoursPerPoint))
            .OrderBy(g => g.Key)
            .Select(g => new TemperaturePoint(g.Key, Math.Round(g.Average(r => r.Temperature), 2), g.Max(r => r.Temperature)))
            .ToList();
        return Task.FromResult<TemperatureHistory?>(
            new TemperatureHistory(freezerId, f.StoreId, _stores[f.StoreId].Region, hoursPerPoint, f.TempMin, f.TempMax, points));
    }

    public Task<IReadOnlyList<WeeklySales>> GetWeeklySalesAsync(
        string? region, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        var weekCut = _today.AddDays(-7);
        var result = _sales
            .Where(s => s.Date > _today.AddDays(-14))
            .GroupBy(s => (s.StoreId, s.Category))
            .Select(g =>
            {
                var store = _stores[g.Key.StoreId];
                var week = g.Where(s => s.Date > weekCut).Sum(s => s.Revenue);
                var previous = g.Where(s => s.Date <= weekCut).Sum(s => s.Revenue);
                decimal? change = previous == 0 ? null : Math.Round(100m * (week / previous - 1), 1, MidpointRounding.AwayFromZero);
                return (store, sales: new WeeklySales(store.Id, store.Name, store.City, store.Region, g.Key.Category, week, previous, change));
            })
            .Where(x => InFilter(x.store, region, excludedStoreIds))
            .Select(x => x.sales)
            .OrderBy(s => s.StoreId).ThenBy(s => s.Category)
            .ToList();
        return Task.FromResult<IReadOnlyList<WeeklySales>>(result);
    }

    public Task<IReadOnlyList<CriticalStockItem>> GetCriticalStockAsync(
        int? storeId, IReadOnlyCollection<int> excludedStoreIds, CancellationToken ct = default)
    {
        var result = _stock
            .Where(s => s.Quantity < s.ReorderPoint)
            .Where(s => storeId is null || s.StoreId == storeId)
            .Where(s => !excludedStoreIds.Contains(s.StoreId))
            .Select(s =>
            {
                var store = _stores[s.StoreId];
                var p = _products[s.ProductId];
                return new CriticalStockItem(store.Id, store.Name, store.City, store.Region, p.Sku, p.Name, p.Category,
                    s.Quantity, s.ReorderPoint, s.LastRestock);
            })
            .OrderBy(i => i.StoreId).ThenBy(i => i.Sku)
            .ToList();
        return Task.FromResult<IReadOnlyList<CriticalStockItem>>(result);
    }

    public Task<IReadOnlyList<MaintenanceTicket>> ListMaintenanceTicketsAsync(string? freezerId, int? storeId, CancellationToken ct = default)
    {
        var result = _tickets
            .Where(t => freezerId is null || t.FreezerId == freezerId)
            .Where(t => storeId is null || t.StoreId == storeId)
            .OrderByDescending(t => t.OpenedOn)
            .ToList();
        return Task.FromResult<IReadOnlyList<MaintenanceTicket>>(result);
    }

    public Task<int?> GetFreezerStoreAsync(string freezerId, CancellationToken ct = default) =>
        Task.FromResult(_freezers.TryGetValue(freezerId, out var f) ? f.StoreId : (int?)null);

    public Task<string?> GetStoreRegionAsync(int storeId, CancellationToken ct = default) =>
        Task.FromResult(_stores.TryGetValue(storeId, out var s) ? s.Region : null);

    public Task<ExecutedAction> RecordActionAsync(NewAction action, CancellationToken ct = default)
    {
        var recorded = new ExecutedAction(Interlocked.Increment(ref _nextAction), action.Type, action.StoreId,
            action.Reference, action.Priority, action.Description, action.ApprovedBy, System.DateTime.UtcNow);
        _actions.Enqueue(recorded);
        return Task.FromResult(recorded);
    }

    public Task<IReadOnlyList<ExecutedAction>> ListActionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExecutedAction>>(_actions.ToList());

    private static bool InFilter(Store store, string? region, IReadOnlyCollection<int> excludedStoreIds) =>
        (region is null || string.Equals(store.Region, region, StringComparison.OrdinalIgnoreCase))
        && !excludedStoreIds.Contains(store.Id);

    // DATEDIFF(HOUR, a, b): number of hour boundaries crossed.
    private static int HourDiff(DateTime a, DateTime b) => (int)(Truncate(b) - Truncate(a)).TotalHours;

    private static DateTime Truncate(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, 0, 0);

    private static DateTime BlockStart(DateTime d, int hours) => d.Date.AddHours(d.Hour / hours * hours);

    // CAST(... AS DECIMAL(5,2)) rounds.
    private static decimal? Round(decimal? value, int digits) =>
        value is null ? null : Math.Round(value.Value, digits, MidpointRounding.AwayFromZero);

    private static class Csv
    {
        public static IEnumerable<Dictionary<string, string>> Read(string path)
        {
            using var reader = new StreamReader(path);
            var header = Fields(reader.ReadLine() ?? throw new InvalidDataException($"Empty CSV: {path}"));
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                var fields = Fields(line);
                var record = new Dictionary<string, string>(header.Count);
                for (var i = 0; i < header.Count; i++) record[header[i]] = i < fields.Count ? fields[i] : "";
                yield return record;
            }
        }

        // Single-line RFC 4180: comma separates, double quotes escape commas and quotes.
        private static List<string> Fields(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else current.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields;
        }

        public static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        public static decimal Dec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
        public static DateOnly Date(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static System.DateTime DateTime(string s) =>
            System.DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
