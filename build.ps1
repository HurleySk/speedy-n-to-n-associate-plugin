# Build Script for Speedy N:N Associate Plugin

Write-Host "`nBuilding Speedy N:N Associate Plugin..." -ForegroundColor Cyan
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Green
dotnet restore

Write-Host "`nBuilding project..." -ForegroundColor Green
dotnet build --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    Write-Host "`nTo install the plugin:" -ForegroundColor Yellow
    Write-Host "  1. Run .\deploy.ps1 -Force" -ForegroundColor White
    Write-Host "  Or manually copy DLLs from bin\Release\net48\ to your XRM ToolBox Plugins folder" -ForegroundColor White
}
else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}
