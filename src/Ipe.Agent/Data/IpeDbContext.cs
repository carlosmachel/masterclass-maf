using System.Text;
using Ipe.Agent.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ipe.Agent.Data;

// Code-first model of the demo database. The schema changes only through migrations (Data/Migrations):
//   dotnet ef migrations add <Name> --project src/Ipe.Agent --output-dir Data/Migrations
//   dotnet ef database update --project src/Ipe.Agent --connection "<connection string>"
// The data itself is not part of the model: apply the migrations (dotnet ef database update) and then data/sql/seed.sql.
public sealed class IpeDbContext(DbContextOptions<IpeDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DailySale> DailySales => Set<DailySale>();
    public DbSet<Freezer> Freezers => Set<Freezer>();
    public DbSet<FreezerReading> FreezerReadings => Set<FreezerReading>();
    public DbSet<StockItem> Stock => Set<StockItem>();
    public DbSet<MaintenanceTicketRow> MaintenanceTickets => Set<MaintenanceTicketRow>();
    public DbSet<ExecutedActionRow> ExecutedActions => Set<ExecutedActionRow>();

    public DbSet<FreezerStatusView> FreezerStatus => Set<FreezerStatusView>();
    public DbSet<WeeklySalesView> WeeklySales => Set<WeeklySalesView>();
    public DbSet<CriticalStockView> CriticalStock => Set<CriticalStockView>();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        // The free-offer database auto-pauses; the first query after a pause gets a transient error (40613).
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

    public static IDbContextFactory<IpeDbContext> CreateFactory(string connectionString)
    {
        var options = new DbContextOptionsBuilder<IpeDbContext>();
        Configure(options, connectionString);
        return new PooledDbContextFactory<IpeDbContext>(options.Options);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Store>(e =>
        {
            e.ToTable("stores").HasKey(x => x.StoreId);
            e.Property(x => x.StoreId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.City).HasMaxLength(60);
            e.Property(x => x.State).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            e.Property(x => x.Region).HasMaxLength(20);
            e.Property(x => x.Manager).HasMaxLength(80);
        });

        model.Entity<Product>(e =>
        {
            e.ToTable("products").HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedNever();
            e.Property(x => x.Sku).HasMaxLength(10).IsUnicode(false);
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        model.Entity<DailySale>(e =>
        {
            e.ToTable("daily_sales").HasKey(x => new { x.SaleDate, x.StoreId, x.Category });
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Revenue).HasPrecision(12, 2);
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        model.Entity<Freezer>(e =>
        {
            e.ToTable("freezers").HasKey(x => x.FreezerId);
            e.Property(x => x.FreezerId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.Model).HasMaxLength(40);
            e.Property(x => x.TempMinC).HasPrecision(4, 1);
            e.Property(x => x.TempMaxC).HasPrecision(4, 1);
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        model.Entity<FreezerReading>(e =>
        {
            e.ToTable("freezer_readings").HasKey(x => new { x.FreezerId, x.ReadAt });
            e.Property(x => x.FreezerId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.ReadAt).HasColumnType("datetime2(0)");
            e.Property(x => x.TemperatureC).HasPrecision(5, 2);
            e.HasOne(x => x.Freezer).WithMany().HasForeignKey(x => x.FreezerId);
        });

        model.Entity<StockItem>(e =>
        {
            e.ToTable("stock").HasKey(x => new { x.StoreId, x.ProductId });
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        model.Entity<MaintenanceTicketRow>(e =>
        {
            e.ToTable("maintenance_tickets").HasKey(x => x.TicketId);
            e.Property(x => x.TicketId).ValueGeneratedNever();
            e.Property(x => x.FreezerId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Status).HasMaxLength(15);
            e.Property(x => x.Description).HasMaxLength(400);
            e.HasOne(x => x.Freezer).WithMany().HasForeignKey(x => x.FreezerId);
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        model.Entity<ExecutedActionRow>(e =>
        {
            e.ToTable("executed_actions").HasKey(x => x.ActionId);
            e.Property(x => x.Type).HasMaxLength(30);          // 'TICKET' | 'EMERGENCY_ORDER'
            e.Property(x => x.Reference).HasMaxLength(40);     // freezer_id or category
            e.Property(x => x.Priority).HasMaxLength(10);
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.ApprovedBy).HasMaxLength(80);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        model.Entity<FreezerStatusView>(e =>
        {
            e.ToView("vw_freezer_status").HasNoKey();
            e.Property(x => x.StoreName).HasColumnName("store");
            e.Property(x => x.Avg24h).HasColumnName("avg_24h");
            e.Property(x => x.Max24h).HasColumnName("max_24h");
            e.Property(x => x.HoursAboveMinus15).HasColumnName("hours_above_minus15");
            e.Property(x => x.Readings24h).HasColumnName("readings_24h");
            e.Property(x => x.LongestGap7dHours).HasColumnName("longest_gap_7d_hours");
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        model.Entity<WeeklySalesView>(e =>
        {
            e.ToView("vw_weekly_sales").HasNoKey();
            e.Property(x => x.StoreName).HasColumnName("store");
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        model.Entity<CriticalStockView>(e =>
        {
            e.ToView("vw_critical_stock").HasNoKey();
            e.Property(x => x.StoreName).HasColumnName("store");
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            // Column names follow the C# names in snake_case (AreaM2 -> area_m2); the exceptions are set above.
            foreach (var property in entity.GetProperties())
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                    property.SetColumnName(SnakeCase(property.Name));

            // No cascades: the data is reloaded as a whole, never deleted row by row.
            foreach (var fk in entity.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static string SnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }
}

// Used only by the dotnet-ef tool (migrations add / script). "database update" receives --connection.
public sealed class IpeDbContextDesignFactory : IDesignTimeDbContextFactory<IpeDbContext>
{
    public IpeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IpeDbContext>();
        IpeDbContext.Configure(options, Environment.GetEnvironmentVariable("IPE_SQL")
            ?? "Server=localhost;Database=ipe;Authentication=Active Directory Default;Encrypt=True");
        return new IpeDbContext(options.Options);
    }
}
