using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ipe.Agent.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "executed_actions",
                columns: table => new
                {
                    action_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    approved_by = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executed_actions", x => x.action_id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "int", nullable: false),
                    sku = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    refrigerated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.product_id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    store_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    city = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    state = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    region = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    manager = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    area_m2 = table.Column<int>(type: "int", nullable: false),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.store_id);
                });

            migrationBuilder.CreateTable(
                name: "daily_sales",
                columns: table => new
                {
                    sale_date = table.Column<DateOnly>(type: "date", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    revenue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    units = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_sales", x => new { x.sale_date, x.store_id, x.category });
                    table.ForeignKey(
                        name: "FK_daily_sales_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "store_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "freezers",
                columns: table => new
                {
                    freezer_id = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    model = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    installed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    temp_min_c = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    temp_max_c = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freezers", x => x.freezer_id);
                    table.ForeignKey(
                        name: "FK_freezers_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "store_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock",
                columns: table => new
                {
                    store_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    reorder_point = table.Column<int>(type: "int", nullable: false),
                    last_restock = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock", x => new { x.store_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_stock_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "store_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "freezer_readings",
                columns: table => new
                {
                    freezer_id = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    read_at = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    temperature_c = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freezer_readings", x => new { x.freezer_id, x.read_at });
                    table.ForeignKey(
                        name: "FK_freezer_readings_freezers_freezer_id",
                        column: x => x.freezer_id,
                        principalTable: "freezers",
                        principalColumn: "freezer_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_tickets",
                columns: table => new
                {
                    ticket_id = table.Column<int>(type: "int", nullable: false),
                    freezer_id = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_tickets", x => x.ticket_id);
                    table.ForeignKey(
                        name: "FK_maintenance_tickets_freezers_freezer_id",
                        column: x => x.freezer_id,
                        principalTable: "freezers",
                        principalColumn: "freezer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_tickets_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "store_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_sales_store_id",
                table: "daily_sales",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_freezers_store_id",
                table: "freezers",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_tickets_freezer_id",
                table: "maintenance_tickets",
                column: "freezer_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_tickets_store_id",
                table: "maintenance_tickets",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_product_id",
                table: "stock",
                column: "product_id");

            // Views: EF does not create views, so the SQL lives here (one batch each).
            // Freezer status over the last 24h, relative to the most recent reading in the database
            // (so the data stays consistent whenever the demo runs). Status values are data, in Portuguese:
            // CRITICO = 2h+ above -15 °C in the last 24h;
            // ATENCAO = high average, temperature rising vs. two weeks ago, or a reading gap > 6h in the last 7 days.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_freezer_status AS
                WITH ref AS (SELECT MAX(read_at) AS now_at FROM dbo.freezer_readings),
                last24 AS (
                    SELECT r.freezer_id,
                           AVG(r.temperature_c) AS avg_24h,
                           MAX(r.temperature_c) AS max_24h,
                           SUM(CASE WHEN r.temperature_c > -15 THEN 1 ELSE 0 END) AS hours_above_minus15,
                           COUNT(*) AS readings_24h
                    FROM dbo.freezer_readings r CROSS JOIN ref
                    WHERE r.read_at > DATEADD(HOUR, -24, ref.now_at)
                    GROUP BY r.freezer_id
                ),
                gaps AS (
                    SELECT x.freezer_id,
                           MAX(DATEDIFF(HOUR, x.previous_at, x.read_at)) AS longest_gap_7d_hours
                    FROM (
                        SELECT r.freezer_id, r.read_at,
                               LAG(r.read_at) OVER (PARTITION BY r.freezer_id ORDER BY r.read_at) AS previous_at
                        FROM dbo.freezer_readings r CROSS JOIN ref
                        WHERE r.read_at > DATEADD(DAY, -7, ref.now_at)
                    ) x
                    GROUP BY x.freezer_id
                ),
                prev AS (
                    SELECT r.freezer_id, AVG(r.temperature_c) AS avg_prev_7d
                    FROM dbo.freezer_readings r CROSS JOIN ref
                    WHERE r.read_at <= DATEADD(DAY, -7, ref.now_at) AND r.read_at > DATEADD(DAY, -14, ref.now_at)
                    GROUP BY r.freezer_id
                )
                SELECT f.freezer_id, f.store_id, s.name AS store, s.city, f.model, f.installed_on,
                       CAST(l.avg_24h AS DECIMAL(5,2)) AS avg_24h,
                       CAST(l.max_24h AS DECIMAL(5,2)) AS max_24h,
                       l.hours_above_minus15,
                       l.readings_24h,
                       CAST(p.avg_prev_7d AS DECIMAL(5,2)) AS avg_two_weeks_ago,
                       g.longest_gap_7d_hours,
                       CASE WHEN l.hours_above_minus15 >= 2 THEN 'CRITICO'
                            WHEN l.avg_24h > -18.5
                              OR l.readings_24h < 20
                              OR g.longest_gap_7d_hours > 6
                              OR l.avg_24h - p.avg_prev_7d > 2 THEN 'ATENCAO'
                            ELSE 'OK' END AS status
                FROM dbo.freezers f
                JOIN dbo.stores s ON s.store_id = f.store_id
                LEFT JOIN last24 l ON l.freezer_id = f.freezer_id
                LEFT JOIN prev p ON p.freezer_id = f.freezer_id
                LEFT JOIN gaps g ON g.freezer_id = f.freezer_id;
                """);

            // Sales of the last 7 days vs. the previous 7 days, per store and category.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_weekly_sales AS
                WITH ref AS (SELECT MAX(sale_date) AS today FROM dbo.daily_sales)
                SELECT d.store_id, s.name AS store, s.city, d.category,
                       SUM(CASE WHEN d.sale_date > DATEADD(DAY, -7, ref.today) THEN d.revenue ELSE 0 END) AS revenue_week,
                       SUM(CASE WHEN d.sale_date <= DATEADD(DAY, -7, ref.today) THEN d.revenue ELSE 0 END) AS revenue_prev_week,
                       CAST(100.0 * (SUM(CASE WHEN d.sale_date > DATEADD(DAY, -7, ref.today) THEN d.revenue ELSE 0 END)
                             / NULLIF(SUM(CASE WHEN d.sale_date <= DATEADD(DAY, -7, ref.today) THEN d.revenue ELSE 0 END), 0) - 1)
                            AS DECIMAL(6,1)) AS change_pct
                FROM dbo.daily_sales d
                JOIN dbo.stores s ON s.store_id = d.store_id
                CROSS JOIN ref
                WHERE d.sale_date > DATEADD(DAY, -14, ref.today)
                GROUP BY d.store_id, s.name, s.city, d.category;
                """);

            // Items below the reorder point.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_critical_stock AS
                SELECT k.store_id, s.name AS store, s.city, p.sku, p.name AS product, p.category,
                       k.quantity, k.reorder_point, k.last_restock
                FROM dbo.stock k
                JOIN dbo.stores s ON s.store_id = k.store_id
                JOIN dbo.products p ON p.product_id = k.product_id
                WHERE k.quantity < k.reorder_point;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_freezer_status;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_weekly_sales;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_critical_stock;");

            migrationBuilder.DropTable(
                name: "daily_sales");

            migrationBuilder.DropTable(
                name: "executed_actions");

            migrationBuilder.DropTable(
                name: "freezer_readings");

            migrationBuilder.DropTable(
                name: "maintenance_tickets");

            migrationBuilder.DropTable(
                name: "stock");

            migrationBuilder.DropTable(
                name: "freezers");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "stores");
        }
    }
}
