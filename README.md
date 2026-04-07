# Speedy N:N Associate

An XrmToolBox plugin for bulk associating records via N:N (many-to-many) relationships in Dynamics 365 / Dataverse.

## Features

- **Generic N:N support** - Works with any N:N relationship, auto-discovered from metadata
- **Smart entity filtering** - Selecting one entity auto-filters the other to only show entities with matching N:N relationships
- **CSV import** - Load record pairs from a two-column CSV file (GUID1, GUID2)
- **FetchXML pairs** - Write a single FetchXML query that returns two ID columns; each row becomes one pair
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
6. Configure settings (parallelism, retries, bypass plugins, logging)
7. Click **Associate**
8. If resuming a previous run, choose to continue or start fresh

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Degree of Parallelism | Auto-detected | Number of concurrent requests |
| Max Retries | 10 | Retry attempts per pair on transient errors |
| Bypass plugins/workflows | Checked | Skip custom plugins for faster processing |
| Log every pair | Checked | Uncheck for errors-only logging |

## CSV Format

Simple two-column CSV with optional header:
```
Guid1,Guid2
a1b2c3d4-...,e5f6a7b8-...
```

Supports both quoted and unquoted GUIDs.

## FetchXML Format

Write a single query that returns rows with two GUID attributes. The first two GUIDs in each row become a pair:

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

## Test Data

Run the test data generator to set up test entities and records:
```powershell
.\test\create-test-data.ps1 -EnvironmentUrl "https://your-org.crm.dynamics.com"
```

## License

MIT
