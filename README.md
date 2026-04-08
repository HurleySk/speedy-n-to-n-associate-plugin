# Speedy N:N Associate

An XrmToolBox plugin for bulk associating records via N:N (many-to-many) relationships in Dynamics 365 / Dataverse.

## Features

- **Generic N:N support** - Works with any N:N relationship, auto-discovered from metadata
- **Smart entity filtering** - Selecting one entity auto-filters the other to only show entities with matching N:N relationships
- **CSV import** - Load record pairs from a two-column CSV file (GUID1, GUID2)
- **FetchXML pairs** - Write a single FetchXML query that returns two ID columns; each row becomes one pair
- **SQL query** - Execute SQL SELECT via the Dataverse TDS endpoint; auto-detects GUID columns from the result set
- **Entity-aware filtering** - FetchXML results are filtered to only include GUIDs matching the selected entities, ignoring unrelated columns (e.g., ownerid)
- **Syntax highlighting** - Color-coded editors for both FetchXML (XML) and SQL with auto-formatting on paste
- **Parallel processing** - Pooled connections with configurable parallelism
- **Tenacious retry** - Up to 50 configurable retries with exponential backoff and shared throttle detection
- **Auto-reconnect** - Broken connections are automatically replaced with fresh clones
- **Resume support** - Tracks completed pairs so interrupted runs can resume
- **Duplicate handling** - Gracefully skips already-associated pairs
- **Performance optimized** - Bypass plugins/workflows option, connection pooling, thread pool tuning

## Installation

### From XrmToolBox Tool Store
Search for "Speedy N:N Associate" in the XrmToolBox Tool Store.

### Manual Installation
1. Build: `dotnet build --configuration Release`
2. Deploy: `.\deploy.ps1 -Force`

## Usage

1. Connect to your Dataverse environment in XrmToolBox
2. Click **Load Entities** to populate entity dropdowns
3. Select Entity 1 -- Entity 2 auto-filters to related entities
4. Select the N:N relationship (auto-populated)
5. Load your data:
   - **CSV tab**: Click "Browse & Load CSV" to select a file with two GUID columns
   - **FetchXML tab**: Write a query returning two IDs (e.g., via `<link-entity>`), click "Preview Pairs"
   - **SQL tab**: Write a SQL SELECT returning two GUID columns, click "Preview Pairs"
6. Configure settings (parallelism, retries, bypass plugins, logging)
7. Click **Associate**
8. If resuming a previous run, choose to continue or start fresh

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Batch Size | 1 | Records per ExecuteMultiple request (1 = individual requests) |
| Degree of Parallelism | Auto-detected | Number of concurrent requests |
| Max Retries | 10 | Retry attempts per pair on transient errors (max 50) |
| Bypass plugins/workflows | Checked | Skip custom plugins for faster processing |
| Log every pair | Checked | Verbose logging; uncheck for errors-only |
| Skip per-item responses | Checked | Fire-and-forget batch mode -- skips per-item response tracking for faster server processing |

## CSV Format

Simple two-column CSV with optional header:
```
Guid1,Guid2
a1b2c3d4-...,e5f6a7b8-...
```

Supports both quoted and unquoted GUIDs.

## FetchXML Format

Write a single query that returns rows with two GUID attributes. When a relationship is selected, the plugin uses entity-aware filtering to match GUIDs to the correct entities -- extra columns (e.g., ownerid) are safely ignored.

```xml
<fetch>
  <entity name="account">
    <attribute name="accountid" />
    <link-entity name="contact" from="parentcustomerid" to="accountid">
      <attribute name="contactid" alias="targetid" />
    </link-entity>
  </entity>
</fetch>
```

Click **Preview Pairs** to execute the query and see a resizable preview grid of extracted pairs.

## SQL Format

Write a SQL SELECT that returns at least two GUID columns. The TDS endpoint must be enabled in your environment (Power Platform Admin Center > Settings > Features).

```sql
SELECT a.accountid, c.contactid
FROM account a
CROSS JOIN contact c
WHERE a.statecode = 0
```

The plugin auto-detects which columns contain GUIDs and uses the first two as the pair. No additional authentication is needed -- reuses the existing XrmToolBox connection.

## Test Data

Run the test data generator to set up test entities and records:
```powershell
.\test\create-test-data.ps1 -EnvironmentUrl "https://your-org.crm.dynamics.com"
```

## License

MIT
