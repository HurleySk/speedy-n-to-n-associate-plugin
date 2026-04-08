<#
.SYNOPSIS
    Generates a CSV file with random GUID pairs for testing the Speedy N:N Associate plugin.

.PARAMETER Count
    Number of GUID pairs to generate. Default: 1000.

.PARAMETER OutputPath
    Path to the output CSV file. Default: test/sample-pairs.csv (relative to repo root).

.EXAMPLE
    .\test\generate-csv.ps1
    .\test\generate-csv.ps1 -Count 50000 -OutputPath "$env:USERPROFILE\Downloads\big-test.csv"
#>
param(
    [int]$Count = 1000,
    [string]$OutputPath = (Join-Path $PSScriptRoot "sample-pairs.csv")
)

$sw = [System.Diagnostics.Stopwatch]::StartNew()

$lines = [System.Collections.Generic.List[string]]::new($Count + 1)
$lines.Add("Guid1,Guid2")

for ($i = 0; $i -lt $Count; $i++) {
    $g1 = [Guid]::NewGuid().ToString()
    $g2 = [Guid]::NewGuid().ToString()
    $lines.Add("$g1,$g2")
}

[System.IO.File]::WriteAllLines($OutputPath, $lines)

$sw.Stop()
Write-Host "Generated $Count pairs in $($sw.ElapsedMilliseconds)ms -> $OutputPath"
