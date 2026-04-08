# =============================================================================
# Test Data Generator for Speedy N:N Associate Plugin
#
# Creates two custom entities with an N:N relationship, populates them with
# test records, and exports GUID pairs to a CSV for plugin testing.
#
# Usage: .\test\create-test-data.ps1
#        .\test\create-test-data.ps1 -EnvironmentUrl "https://org.crm.dynamics.com"
#        .\test\create-test-data.ps1 -Entity1Name "project" -Entity2Name "resource" -Entity1DisplayName "Project" -Entity2DisplayName "Resource"
# =============================================================================

param(
    [string]$EnvironmentUrl,
    [int]$RecordsPerEntity = 20,
    [string]$PublisherPrefix = "spdy",
    [string]$Entity1Name = "testwidget",
    [string]$Entity2Name = "testgadget",
    [string]$Entity1DisplayName = "Test Widget",
    [string]$Entity2DisplayName = "Test Gadget"
)

$ErrorActionPreference = "Stop"

# --- Install/Import SDK if needed ---
$sdkModule = Get-Module -ListAvailable -Name "Microsoft.Xrm.Tooling.CrmConnector.PowerShell" -ErrorAction SilentlyContinue
if (-not $sdkModule) {
    Write-Host "Installing Dataverse SDK PowerShell module..." -ForegroundColor Yellow
    Install-Module -Name "Microsoft.Xrm.Tooling.CrmConnector.PowerShell" -Scope CurrentUser -Force -AllowClobber
}
Import-Module "Microsoft.Xrm.Tooling.CrmConnector.PowerShell" -ErrorAction Stop

# --- Get environment URL ---
if (-not $EnvironmentUrl) {
    Write-Host "`n=== Speedy N:N Associate - Test Data Generator ===" -ForegroundColor Cyan
    $EnvironmentUrl = Read-Host "`nEnter your Dataverse environment URL (e.g., https://myorg.crm.dynamics.com)"
}

$EnvironmentUrl = $EnvironmentUrl.TrimEnd("/")
Write-Host "`nConnecting to: $EnvironmentUrl" -ForegroundColor Green

# --- Connect via interactive login ---
$connString = "AuthType=OAuth;Url=$EnvironmentUrl;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;LoginPrompt=Auto"
$conn = Get-CrmConnection -ConnectionString $connString

if (-not $conn -or -not $conn.IsReady) {
    Write-Host "Failed to connect! Check your URL and credentials." -ForegroundColor Red
    exit 1
}

Write-Host "Connected successfully!" -ForegroundColor Green
Write-Host "Org: $($conn.ConnectedOrgFriendlyName)" -ForegroundColor Gray

# --- Entity and relationship names ---
$entity1LogicalName = "${PublisherPrefix}_${Entity1Name}"
$entity2LogicalName = "${PublisherPrefix}_${Entity2Name}"
$relationshipSchemaName = "${PublisherPrefix}_${Entity1Name}_${Entity2Name}"
$intersectEntityName = "${PublisherPrefix}_${Entity1Name}_${Entity2Name}_int"

# Dataverse enforces 128-char max on entity logical names
if ($intersectEntityName.Length -gt 128) {
    Write-Host "ERROR: Intersect entity name '$intersectEntityName' exceeds 128-character limit ($($intersectEntityName.Length) chars). Use shorter entity names." -ForegroundColor Red
    exit 1
}

# ==========================================================================
# Step 1: Create a publisher (or find existing one)
# ==========================================================================
Write-Host "`n--- Step 1: Publisher ---" -ForegroundColor Cyan

$publisherFetch = @"
<fetch>
  <entity name='publisher'>
    <attribute name='publisherid' />
    <attribute name='customizationprefix' />
    <filter>
      <condition attribute='customizationprefix' operator='eq' value='$PublisherPrefix' />
    </filter>
  </entity>
</fetch>
"@

$existingPublisher = Get-CrmRecordsByFetch -conn $conn -Fetch $publisherFetch
if ($existingPublisher.Count -gt 0) {
    $publisherId = $existingPublisher.CrmRecords[0].publisherid
    Write-Host "  Found existing publisher with prefix '$PublisherPrefix' ($publisherId)" -ForegroundColor Green
}
else {
    Write-Host "  Creating publisher with prefix '$PublisherPrefix'..." -ForegroundColor Yellow
    $pubFields = @{
        "uniquename"            = "speedyntontest"
        "friendlyname"          = "Speedy NtoN Test Publisher"
        "customizationprefix"   = $PublisherPrefix
        "customizationoptionvalueprefix" = 18432
    }
    $publisherId = New-CrmRecord -conn $conn -EntityLogicalName "publisher" -Fields $pubFields
    Write-Host "  Created publisher ($publisherId)" -ForegroundColor Green
}

# ==========================================================================
# Step 2: Create a solution
# ==========================================================================
Write-Host "`n--- Step 2: Solution ---" -ForegroundColor Cyan

$solutionUniqueName = "SpeedyNtoNTestSolution"
$solFetch = @"
<fetch>
  <entity name='solution'>
    <attribute name='solutionid' />
    <filter>
      <condition attribute='uniquename' operator='eq' value='$solutionUniqueName' />
    </filter>
  </entity>
</fetch>
"@

$existingSolution = Get-CrmRecordsByFetch -conn $conn -Fetch $solFetch
if ($existingSolution.Count -gt 0) {
    $solutionId = $existingSolution.CrmRecords[0].solutionid
    Write-Host "  Found existing solution '$solutionUniqueName' ($solutionId)" -ForegroundColor Green
}
else {
    Write-Host "  Creating solution '$solutionUniqueName'..." -ForegroundColor Yellow
    $solFields = @{
        "uniquename"   = $solutionUniqueName
        "friendlyname" = "Speedy NtoN Test Solution"
        "version"      = "1.0.0.0"
        "publisherid"  = (New-Object Microsoft.Xrm.Sdk.EntityReference("publisher", $publisherId))
    }
    $solutionId = New-CrmRecord -conn $conn -EntityLogicalName "solution" -Fields $solFields
    Write-Host "  Created solution ($solutionId)" -ForegroundColor Green
}

# ==========================================================================
# Step 3: Create entities (if they don't exist)
# ==========================================================================
Write-Host "`n--- Step 3: Entities ---" -ForegroundColor Cyan

function Test-EntityExists($logicalName) {
    try {
        $metadata = Get-CrmEntityMetadata -conn $conn -EntityLogicalName $logicalName -EntityFilters Entity -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function New-TestEntity($logicalName, $displayName, $displayNamePlural) {
    Write-Host "  Creating entity '$logicalName' ($displayName)..." -ForegroundColor Yellow

    $request = New-Object Microsoft.Xrm.Sdk.Messages.CreateEntityRequest
    $request.SolutionUniqueName = $solutionUniqueName

    $entityMetadata = New-Object Microsoft.Xrm.Sdk.Metadata.EntityMetadata
    $entityMetadata.SchemaName = $logicalName
    $entityMetadata.LogicalName = $logicalName
    $entityMetadata.DisplayName = New-Object Microsoft.Xrm.Sdk.Label($displayName, 1033)
    $entityMetadata.DisplayCollectionName = New-Object Microsoft.Xrm.Sdk.Label($displayNamePlural, 1033)
    $entityMetadata.Description = New-Object Microsoft.Xrm.Sdk.Label("Test entity for Speedy N:N Associate plugin", 1033)
    $entityMetadata.OwnershipType = [Microsoft.Xrm.Sdk.Metadata.OwnershipTypes]::UserOwned
    $entityMetadata.IsActivity = $false

    $request.Entity = $entityMetadata

    # Primary name attribute
    $primaryAttr = New-Object Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata
    $primaryAttr.SchemaName = "${logicalName}name"
    $primaryAttr.LogicalName = "${logicalName}name"
    $primaryAttr.DisplayName = New-Object Microsoft.Xrm.Sdk.Label("Name", 1033)
    $primaryAttr.RequiredLevel = New-Object Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevelManagedProperty([Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevel]::None)
    $primaryAttr.MaxLength = 200
    $request.PrimaryAttribute = $primaryAttr

    $response = $conn.Execute($request)
    Write-Host "  Created entity '$logicalName'" -ForegroundColor Green
}

$entity1Exists = Test-EntityExists $entity1LogicalName
$entity2Exists = Test-EntityExists $entity2LogicalName

if (-not $entity1Exists) {
    New-TestEntity $entity1LogicalName $Entity1DisplayName "${Entity1DisplayName}s"
}
else {
    Write-Host "  Entity '$entity1LogicalName' already exists." -ForegroundColor Green
}

if (-not $entity2Exists) {
    New-TestEntity $entity2LogicalName $Entity2DisplayName "${Entity2DisplayName}s"
}
else {
    Write-Host "  Entity '$entity2LogicalName' already exists." -ForegroundColor Green
}

# ==========================================================================
# Step 4: Create N:N relationship (if it doesn't exist)
# ==========================================================================
Write-Host "`n--- Step 4: N:N Relationship ---" -ForegroundColor Cyan

$relExists = $false
try {
    $entity1Meta = Get-CrmEntityMetadata -conn $conn -EntityLogicalName $entity1LogicalName -EntityFilters Relationships
    foreach ($rel in $entity1Meta.ManyToManyRelationships) {
        if ($rel.SchemaName -eq $relationshipSchemaName) {
            $relExists = $true
            break
        }
    }
}
catch { }

if (-not $relExists) {
    Write-Host "  Creating N:N relationship '$relationshipSchemaName'..." -ForegroundColor Yellow

    $request = New-Object Microsoft.Xrm.Sdk.Messages.CreateManyToManyRequest
    $request.SolutionUniqueName = $solutionUniqueName
    $request.IntersectEntitySchemaName = $intersectEntityName

    $relationship = New-Object Microsoft.Xrm.Sdk.Metadata.ManyToManyRelationshipMetadata
    $relationship.SchemaName = $relationshipSchemaName
    $relationship.Entity1LogicalName = $entity1LogicalName
    $relationship.Entity2LogicalName = $entity2LogicalName
    $relationship.IntersectEntityName = $intersectEntityName
    $relationship.Entity1AssociatedMenuConfiguration = New-Object Microsoft.Xrm.Sdk.Metadata.AssociatedMenuConfiguration
    $relationship.Entity1AssociatedMenuConfiguration.Behavior = [Microsoft.Xrm.Sdk.Metadata.AssociatedMenuBehavior]::UseLabel
    $relationship.Entity1AssociatedMenuConfiguration.Label = New-Object Microsoft.Xrm.Sdk.Label("${Entity2DisplayName}s", 1033)
    $relationship.Entity2AssociatedMenuConfiguration = New-Object Microsoft.Xrm.Sdk.Metadata.AssociatedMenuConfiguration
    $relationship.Entity2AssociatedMenuConfiguration.Behavior = [Microsoft.Xrm.Sdk.Metadata.AssociatedMenuBehavior]::UseLabel
    $relationship.Entity2AssociatedMenuConfiguration.Label = New-Object Microsoft.Xrm.Sdk.Label("${Entity1DisplayName}s", 1033)

    $request.ManyToManyRelationship = $relationship

    $response = $conn.Execute($request)
    Write-Host "  Created N:N relationship '$relationshipSchemaName'" -ForegroundColor Green
}
else {
    Write-Host "  N:N relationship '$relationshipSchemaName' already exists." -ForegroundColor Green
}

# Only publish if we created something new
if (-not $entity1Exists -or -not $entity2Exists -or -not $relExists) {
    Write-Host "`n  Publishing customizations..." -ForegroundColor Yellow
    $publishRequest = New-Object Microsoft.Crm.Sdk.Messages.PublishAllXmlRequest
    $conn.Execute($publishRequest) | Out-Null
    Write-Host "  Published." -ForegroundColor Green
}
else {
    Write-Host "`n  Skipping publish (nothing new to publish)." -ForegroundColor Gray
}

# ==========================================================================
# Step 5: Create test records
# ==========================================================================
Write-Host "`n--- Step 5: Creating test records ($RecordsPerEntity per entity) ---" -ForegroundColor Cyan

# Get primary name attributes
$e1Meta = Get-CrmEntityMetadata -conn $conn -EntityLogicalName $entity1LogicalName -EntityFilters Entity
$e2Meta = Get-CrmEntityMetadata -conn $conn -EntityLogicalName $entity2LogicalName -EntityFilters Entity
$entity1PrimaryAttr = $e1Meta.PrimaryNameAttribute
$entity2PrimaryAttr = $e2Meta.PrimaryNameAttribute

$entity1Ids = @()
$entity2Ids = @()

# Create Entity 1 records
Write-Host "`n  Creating $Entity1DisplayName records..." -ForegroundColor Yellow
for ($i = 1; $i -le $RecordsPerEntity; $i++) {
    $name = "$Entity1DisplayName $i"
    try {
        $id = New-CrmRecord -conn $conn -EntityLogicalName $entity1LogicalName -Fields @{
            $entity1PrimaryAttr = $name
        }
        $entity1Ids += $id.Guid
        Write-Host "    [$i/$RecordsPerEntity] $name ($($id.Guid))" -ForegroundColor Gray
    }
    catch {
        Write-Host "    [$i/$RecordsPerEntity] FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Create Entity 2 records
Write-Host "`n  Creating $Entity2DisplayName records..." -ForegroundColor Yellow
for ($i = 1; $i -le $RecordsPerEntity; $i++) {
    $name = "$Entity2DisplayName $i"
    try {
        $id = New-CrmRecord -conn $conn -EntityLogicalName $entity2LogicalName -Fields @{
            $entity2PrimaryAttr = $name
        }
        $entity2Ids += $id.Guid
        Write-Host "    [$i/$RecordsPerEntity] $name ($($id.Guid))" -ForegroundColor Gray
    }
    catch {
        Write-Host "    [$i/$RecordsPerEntity] FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n  Created $($entity1Ids.Count) ${Entity1DisplayName}s and $($entity2Ids.Count) ${Entity2DisplayName}s." -ForegroundColor Green

# ==========================================================================
# Step 6: Generate random pairs and export CSV
# ==========================================================================
Write-Host "`n--- Step 6: Generating test pairs ---" -ForegroundColor Cyan

$pairs = @()
$seen = @{}

$maxPairs = $entity1Ids.Count * $entity2Ids.Count
$targetPairs = [Math]::Max([Math]::Floor($maxPairs * 0.5), [Math]::Min(10, $maxPairs))
$rng = New-Object System.Random

$attempts = 0
while ($pairs.Count -lt $targetPairs -and $attempts -lt $maxPairs * 3) {
    $attempts++
    $idx1 = $rng.Next($entity1Ids.Count)
    $idx2 = $rng.Next($entity2Ids.Count)
    $g1 = $entity1Ids[$idx1]
    $g2 = $entity2Ids[$idx2]

    $key = "$g1|$g2"
    if (-not $seen.ContainsKey($key)) {
        $seen[$key] = $true
        $pairs += [PSCustomObject]@{
            Guid1 = $g1
            Guid2 = $g2
        }
    }
}

Write-Host "  Generated $($pairs.Count) unique pairs." -ForegroundColor Green

# Export CSV to Downloads folder
$downloadsDir = [Environment]::GetFolderPath("UserProfile") + "\Downloads"
$csvPath = Join-Path $downloadsDir "test-pairs.csv"

$pairs | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "  CSV exported to: $csvPath" -ForegroundColor Green

# ==========================================================================
# Summary
# ==========================================================================
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Environment:   $($conn.ConnectedOrgFriendlyName)" -ForegroundColor White
Write-Host "  Entity 1:      $entity1LogicalName ($Entity1DisplayName) - $($entity1Ids.Count) records" -ForegroundColor White
Write-Host "  Entity 2:      $entity2LogicalName ($Entity2DisplayName) - $($entity2Ids.Count) records" -ForegroundColor White
Write-Host "  Relationship:  $relationshipSchemaName" -ForegroundColor White
Write-Host "  Pairs:         $($pairs.Count)" -ForegroundColor White
Write-Host "  CSV:           $csvPath" -ForegroundColor White
# Write FetchXML queries for testing the FetchXML tab
$fetchXml1 = @"
<fetch>
  <entity name="$entity1LogicalName">
    <attribute name="${entity1LogicalName}id" />
    <filter>
      <condition attribute="$entity1PrimaryAttr" operator="like" value="$Entity1DisplayName%" />
    </filter>
  </entity>
</fetch>
"@

$fetchXml2 = @"
<fetch>
  <entity name="$entity2LogicalName">
    <attribute name="${entity2LogicalName}id" />
    <filter>
      <condition attribute="$entity2PrimaryAttr" operator="like" value="$Entity2DisplayName%" />
    </filter>
  </entity>
</fetch>
"@

# Save FetchXML to Downloads folder for easy copy-paste
$fetchPath1 = Join-Path $downloadsDir "fetchxml-source.xml"
$fetchPath2 = Join-Path $downloadsDir "fetchxml-target.xml"
$fetchXml1 | Out-File -FilePath $fetchPath1 -Encoding utf8
$fetchXml2 | Out-File -FilePath $fetchPath2 -Encoding utf8

Write-Host "`n=== FetchXML Queries (for testing FetchXML tab) ===" -ForegroundColor Cyan
Write-Host "`nSource FetchXML ($Entity1DisplayName):" -ForegroundColor Yellow
Write-Host $fetchXml1 -ForegroundColor Gray
Write-Host "`nTarget FetchXML ($Entity2DisplayName):" -ForegroundColor Yellow
Write-Host $fetchXml2 -ForegroundColor Gray
Write-Host "`nSaved to:" -ForegroundColor Green
Write-Host "  $fetchPath1" -ForegroundColor White
Write-Host "  $fetchPath2" -ForegroundColor White

# Generate SQL query
$sqlQuery = @"
SELECT w.${entity1LogicalName}id, g.${entity2LogicalName}id
FROM $entity1LogicalName w
CROSS JOIN $entity2LogicalName g
WHERE w.statecode = 0
  AND g.statecode = 0
"@

$sqlPath = Join-Path $downloadsDir "sql-query.sql"
$sqlQuery | Out-File -FilePath $sqlPath -Encoding utf8

Write-Host "`n=== SQL Query (for testing SQL tab -- requires TDS endpoint) ===" -ForegroundColor Cyan
Write-Host $sqlQuery -ForegroundColor Gray
Write-Host "`nSaved to: $sqlPath" -ForegroundColor Green

Write-Host "`n=== Next Steps ===" -ForegroundColor Yellow
Write-Host "  1. Open XrmToolBox and connect to $EnvironmentUrl" -ForegroundColor Gray
Write-Host "  2. Open 'Speedy N:N Associate'" -ForegroundColor Gray
Write-Host "  3. Load Entities, select '$entity1LogicalName' and '$entity2LogicalName'" -ForegroundColor Gray
Write-Host "  4. Select relationship: $relationshipSchemaName" -ForegroundColor Gray
Write-Host "`n  To test CSV:" -ForegroundColor White
Write-Host "  5a. CSV tab -> Browse -> $csvPath" -ForegroundColor Gray
Write-Host "  5b. Click Start!" -ForegroundColor Gray
Write-Host "`n  To test FetchXML:" -ForegroundColor White
Write-Host "  5a. FetchXML tab -> paste source and target queries from above" -ForegroundColor Gray
Write-Host "  5b. Click Preview Pairs, then Start!" -ForegroundColor Gray
