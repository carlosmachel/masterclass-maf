# Demo data: Rede Ipê Supermercados (fictitious)

Synthetic dataset for the Microsoft Agent Framework masterclass: 20 stores, 90 days of sales, hourly readings from 45 freezers, stock, maintenance tickets and 4 documents for Foundry IQ. Three anomalies were planted on purpose, and `GABARITO.md` (in Portuguese) describes what the agent should find.

The schema is **code-first**: the tables, the views `vw_freezer_status`, `vw_weekly_sales` and `vw_critical_stock`, and `executed_actions` (what the agent executes after "Aprovar") are created by the EF Core migrations in `src/Ipe.Agent/Data/Migrations`. This folder only holds the data.

Identifiers (tables, columns, files) are in English. The data itself (store names, categories, statuses such as `CRITICO`, and the documents) is in Portuguese, because that is what the audience sees.

## Contents
| Folder/file | What it is |
|---|---|
| `sql/seed.sql` | Data: clears the data tables, then INSERTs in batches of 1000 (can be rerun) |
| `csv/` | The same data as CSV (for bcp, Fabric later, or the local plan B) |
| `docs/` | Manual, committee minutes, restock policy and promotion calendar, to upload to Blob and build the Foundry IQ knowledge base |
| `attack/` | The poisoned document used in the live attack (step 5) and in the red-team eval |
| `GABARITO.md` | The 3 stories and the stage questions |
| `generate_data.py` | Regenerates everything (`--end YYYY-MM-DD` pins the date) |

## Step by step
1. **The day before the event**, regenerate the data so that "this week" is the real week:
   `python generate_data.py --out .`
2. **Azure SQL:** create a database with Entra ID-only authentication (the free offer is enough), point `ConnectionStrings:Ipe` at it, run `dotnet tool restore` and `dotnet ef database update --project src/Ipe.Agent`, then load `sql/seed.sql` (for example with `sqlcmd --authentication-method ActiveDirectoryDefault -i sql/seed.sql`).
   To change the schema: edit the entities in `src/Ipe.Agent/Data/` and run `dotnet ef migrations add <Name> --project src/Ipe.Agent --output-dir Data/Migrations` (the `dotnet-ef` tool is pinned in `.config/dotnet-tools.json`; `dotnet tool restore` installs it).
3. **Agent access (managed identity, no connection string with a password):** in the database, run
   ```sql
   CREATE USER [<app-identity-name>] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [<app-identity-name>];
   GRANT INSERT, SELECT ON dbo.executed_actions TO [<app-identity-name>];  -- approved actions
   ```
   In code, use `Authentication=Active Directory Default` in the connection string; it follows the same chain as `DefaultAzureCredential`.
4. **Foundry IQ:** create Azure AI Search (Free) and a Blob container with the files in `docs/`, give AI Search's managed identity read access to the storage account, then run `src/Ipe.FoundryIQ.Setup` (usage at the top of its `Program.cs`) to create the `ipe-conhecimento` knowledge base. For the app in production, give its identity *Search Index Data Reader* on AI Search.

## Agent tools
The views already deliver what the stage needs, so each tool is a simple LINQ query in `SqlOperationsRepository`:
- `GetFreezerStatus()` → `vw_freezer_status`
- `GetWeeklySales(region?)` → `vw_weekly_sales`
- `GetCriticalStock(storeId?)` → `vw_critical_stock`
- `GetTemperatureHistory(freezerId, days)` → `freezer_readings`, for the dashboard line chart
- `ListMaintenanceTickets(freezerId?)` → `maintenance_tickets`
- `OpenMaintenanceTicket(...)` and `CreateEmergencyOrder(...)`, with **human approval** on the front end

The views compute "now" from the most recent data in the database, so the demo doesn't break if it runs days after the load.
