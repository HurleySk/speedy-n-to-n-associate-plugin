# XRM ToolBox Plugin Build & Deploy Script
# Builds and deploys your plugin to XRM ToolBox

param(
    [switch]$SkipBuild,
    [switch]$Force,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# File list - add your plugin's DLL and any extra dependency DLLs here
$pluginFiles = @(
    "SpeedyNtoNAssociatePlugin.dll",
    "SpeedyNtoNAssociatePlugin.pdb"
)

# NuGet dependency DLLs that need to be copied (SQLite for resume tracking)
$dependencyFiles = @(
    "Microsoft.Data.Sqlite.dll",
    "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.core.dll",
    "SQLitePCLRaw.provider.dynamic_cdecl.dll"
)

# Native runtime DLLs (SQLite native binaries) - require directory structure
$nativeRuntimes = @(
    @{ Source = "runtimes\win-x64\native\e_sqlite3.dll"; RelPath = "runtimes\win-x64\native" },
    @{ Source = "runtimes\win-x86\native\e_sqlite3.dll"; RelPath = "runtimes\win-x86\native" },
    @{ Source = "runtimes\win-arm\native\e_sqlite3.dll"; RelPath = "runtimes\win-arm\native" }
)

Write-Host "`n=== XRM ToolBox Plugin Deployment ===" -ForegroundColor Cyan

# Check if XRM ToolBox is running
Write-Host "`nChecking for running XRM ToolBox processes..." -ForegroundColor Yellow
$xrmProcesses = Get-Process -Name "XrmToolBox" -ErrorAction SilentlyContinue
$shouldRelaunch = $false

if ($xrmProcesses) {
    Write-Host "  WARNING: XRM ToolBox is currently running!" -ForegroundColor Red
    Write-Host "  Found $($xrmProcesses.Count) process(es) with PID(s): $($xrmProcesses.Id -join ', ')" -ForegroundColor Yellow

    if ($Force) {
        Write-Host "  -Force specified, attempting to close XRM ToolBox..." -ForegroundColor Yellow
        foreach ($proc in $xrmProcesses) {
            try {
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Milliseconds 500
                if (!$proc.HasExited) {
                    $proc.Kill()
                }
                Write-Host "  Closed process $($proc.Id)" -ForegroundColor Green
                $shouldRelaunch = $true
            }
            catch {
                Write-Host "  Failed to close process $($proc.Id): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        Start-Sleep -Seconds 2
    }
    else {
        Write-Host "`n  Please close XRM ToolBox before deploying, or use -Force to close automatically." -ForegroundColor Yellow
        Write-Host "  Press Ctrl+C to cancel, or press Enter to continue anyway..." -ForegroundColor Gray
        Read-Host
    }
}
else {
    Write-Host "  No running XRM ToolBox processes found." -ForegroundColor Green
}

# Build
if (-not $SkipBuild) {
    Write-Host "`nBuilding plugin..." -ForegroundColor Green

    if ($WhatIf) {
        Write-Host "  [WhatIf] Would run: dotnet clean" -ForegroundColor Gray
        Write-Host "  [WhatIf] Would run: dotnet build" -ForegroundColor Gray
    }
    else {
        dotnet clean SpeedyNtoNAssociatePlugin.sln --configuration Release --verbosity quiet
        dotnet build SpeedyNtoNAssociatePlugin.sln --configuration Release

        if ($LASTEXITCODE -ne 0) {
            Write-Host "`nBuild failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Build successful!" -ForegroundColor Green
    }
}
else {
    Write-Host "`nSkipping build (using existing binaries)..." -ForegroundColor Yellow
}

# Verify build output exists
$buildPath = "bin\Release\net48"
if (-not (Test-Path "$buildPath\SpeedyNtoNAssociatePlugin.dll")) {
    Write-Host "`nError: Build output not found at $buildPath\SpeedyNtoNAssociatePlugin.dll" -ForegroundColor Red
    exit 1
}

# Deployment paths
Write-Host "`nDetecting deployment paths..." -ForegroundColor Green
$pluginsPath = "$env:APPDATA\MscrmTools\XrmToolBox\Plugins"
Write-Host "  Primary: $pluginsPath" -ForegroundColor White

# Auto-detect OneDrive paths
$oneDrivePaths = @()
$possibleOneDrivePaths = @(
    "$env:USERPROFILE\OneDrive\Documents\XrmToolbox",
    "$env:USERPROFILE\OneDrive - *\Documents\XrmToolbox",
    "$env:OneDrive\Documents\XrmToolbox",
    "$env:OneDriveCommercial\Documents\XrmToolbox"
)

foreach ($pattern in $possibleOneDrivePaths) {
    $resolved = Resolve-Path $pattern -ErrorAction SilentlyContinue
    if ($resolved) {
        $oneDrivePaths += $resolved.Path
    }
}

if ($oneDrivePaths.Count -gt 0) {
    Write-Host "  OneDrive locations found:" -ForegroundColor Yellow
    foreach ($path in $oneDrivePaths) {
        Write-Host "    - $path" -ForegroundColor Gray
    }
}

# Function to clean files from a path
function Remove-PluginFiles {
    param([string]$Path, [string]$Location)

    if (-not (Test-Path $Path)) {
        return
    }

    Write-Host "  - Cleaning $Location..." -ForegroundColor Gray

    $filesToRemove = @(
        "$Path\SpeedyNtoNAssociatePlugin*"
    )

    foreach ($filePattern in $filesToRemove) {
        try {
            if ($WhatIf) {
                $files = Get-Item $filePattern -ErrorAction SilentlyContinue
                if ($files) {
                    Write-Host "    [WhatIf] Would remove: $($files.Name -join ', ')" -ForegroundColor Gray
                }
            }
            else {
                Remove-Item $filePattern -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            Write-Host "    Warning: Could not remove $filePattern - $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# Clean all locations
Write-Host "`nCleaning old plugin files..." -ForegroundColor Green
Remove-PluginFiles -Path $pluginsPath -Location "AppData"
foreach ($oneDrivePath in $oneDrivePaths) {
    Remove-PluginFiles -Path $oneDrivePath -Location "OneDrive"
}

# Copy plugin files
Write-Host "`nCopying plugin files..." -ForegroundColor Green
$deploymentFailed = $false

foreach ($file in $pluginFiles) {
    $sourcePath = Join-Path $buildPath $file

    if (-not (Test-Path $sourcePath)) {
        if ($file -like "*.pdb") {
            Write-Host "  - Skipping $file (not found, optional)" -ForegroundColor Gray
            continue
        }
        Write-Host "  - ERROR: Source file not found: $file" -ForegroundColor Red
        $deploymentFailed = $true
        continue
    }

    try {
        if ($WhatIf) {
            Write-Host "  [WhatIf] Would copy: $file" -ForegroundColor Gray
        }
        else {
            Copy-Item $sourcePath -Destination $pluginsPath -Force -ErrorAction Stop
            Write-Host "  - Copied $file" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  - FAILED to copy $file`: $($_.Exception.Message)" -ForegroundColor Red
        $deploymentFailed = $true
    }
}

# Copy dependencies from build output
if ($dependencyFiles.Count -gt 0) {
    Write-Host "`nCopying dependencies..." -ForegroundColor Green
    foreach ($dep in $dependencyFiles) {
        $sourcePath = Join-Path $buildPath $dep

        if (Test-Path $sourcePath) {
            try {
                if ($WhatIf) {
                    Write-Host "  [WhatIf] Would copy: $dep" -ForegroundColor Gray
                }
                else {
                    Copy-Item $sourcePath -Destination $pluginsPath -Force -ErrorAction Stop
                    Write-Host "  - Copied $dep" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "  - FAILED to copy $dep`: $($_.Exception.Message)" -ForegroundColor Red
                $deploymentFailed = $true
            }
        }
        else {
            Write-Host "  - ERROR: $dep not found in build output!" -ForegroundColor Red
            $deploymentFailed = $true
        }
    }
}

# Copy native runtime DLLs (preserving directory structure)
if ($nativeRuntimes.Count -gt 0) {
    Write-Host "`nCopying native runtimes..." -ForegroundColor Green
    foreach ($rt in $nativeRuntimes) {
        $sourcePath = Join-Path $buildPath $rt.Source
        $destDir = Join-Path $pluginsPath $rt.RelPath
        $fileName = Split-Path $rt.Source -Leaf

        if (Test-Path $sourcePath) {
            try {
                if ($WhatIf) {
                    Write-Host "  [WhatIf] Would copy: $($rt.Source)" -ForegroundColor Gray
                }
                else {
                    if (-not (Test-Path $destDir)) {
                        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                    }
                    Copy-Item $sourcePath -Destination $destDir -Force -ErrorAction Stop
                    Write-Host "  - Copied $($rt.Source)" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "  - FAILED to copy $fileName`: $($_.Exception.Message)" -ForegroundColor Red
                $deploymentFailed = $true
            }
        }
        else {
            Write-Host "  - WARNING: $($rt.Source) not found (may not be needed on this platform)" -ForegroundColor Yellow
        }
    }
}

# Verify deployment
Write-Host "`nVerifying deployment..." -ForegroundColor Green
$allRequiredFiles = $pluginFiles + $dependencyFiles
$allFilesPresent = $true

foreach ($file in $allRequiredFiles) {
    if ($file -like "*.pdb") {
        continue  # PDB files are optional
    }

    $filePath = Join-Path $pluginsPath $file
    if (Test-Path $filePath) {
        $fileInfo = Get-Item $filePath
        Write-Host "  [OK] $file ($([math]::Round($fileInfo.Length / 1KB, 0)) KB)" -ForegroundColor Green
    }
    else {
        Write-Host "  [MISSING] $file" -ForegroundColor Red
        $allFilesPresent = $false
    }
}

# Delete manifest to force rescan (only if deployment succeeded)
if ($allFilesPresent -and -not $deploymentFailed) {
    Write-Host "`nRemoving manifest cache to force plugin rescan..." -ForegroundColor Gray
    if ($WhatIf) {
        Write-Host "  [WhatIf] Would remove: $pluginsPath\manifest.json" -ForegroundColor Gray
    }
    else {
        Remove-Item "$pluginsPath\manifest.json" -Force -ErrorAction SilentlyContinue
    }
}

# Summary
Write-Host "`n=== Deployment Summary ===" -ForegroundColor Cyan
if ($allFilesPresent -and -not $deploymentFailed) {
    Write-Host "Status: " -NoNewline
    Write-Host "SUCCESS" -ForegroundColor Green
    Write-Host "`nPlugin deployed to: $pluginsPath" -ForegroundColor White

    if ($oneDrivePaths.Count -gt 0) {
        Write-Host "`nNote: Old plugin files were removed from OneDrive locations." -ForegroundColor Yellow
    }

    # Relaunch XRM ToolBox if we closed it
    if ($shouldRelaunch -and -not $WhatIf) {
        Write-Host "`nRelaunching XRM ToolBox..." -ForegroundColor Yellow

        $possibleXrmPaths = @(
            "$env:APPDATA\MscrmTools\XrmToolBox\XrmToolBox.exe",
            "$env:USERPROFILE\Documents\XrmToolbox\XrmToolBox.exe",
            "$env:USERPROFILE\OneDrive\Documents\XrmToolbox\XrmToolBox.exe",
            "$env:USERPROFILE\OneDrive - *\Documents\XrmToolbox\XrmToolBox.exe",
            "$env:OneDrive\Documents\XrmToolbox\XrmToolBox.exe",
            "$env:OneDriveCommercial\Documents\XrmToolbox\XrmToolBox.exe"
        )

        $xrmToolBoxPath = $null
        foreach ($pattern in $possibleXrmPaths) {
            $resolved = Resolve-Path $pattern -ErrorAction SilentlyContinue
            if ($resolved) {
                $xrmToolBoxPath = $resolved.Path | Select-Object -First 1
                break
            }
        }

        if ($xrmToolBoxPath) {
            try {
                Start-Process $xrmToolBoxPath
                Write-Host "  XRM ToolBox launched successfully!" -ForegroundColor Green
            }
            catch {
                Write-Host "  Failed to launch XRM ToolBox: $($_.Exception.Message)" -ForegroundColor Red
                Write-Host "  Please start it manually from: $xrmToolBoxPath" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  XRM ToolBox executable not found in common locations." -ForegroundColor Yellow
            Write-Host "  Please start it manually." -ForegroundColor Gray
        }
    }
    elseif (-not $shouldRelaunch) {
        Write-Host "`nNext steps:" -ForegroundColor Yellow
        Write-Host "  1. Start XRM ToolBox" -ForegroundColor White
        Write-Host "  2. Connect to your environment" -ForegroundColor White
        Write-Host "  3. Look for 'Speedy N:N Associate' in Tools menu" -ForegroundColor White
    }

    exit 0
}
else {
    Write-Host "Status: " -NoNewline
    Write-Host "FAILED" -ForegroundColor Red
    Write-Host "`nDeployment incomplete. Check errors above." -ForegroundColor Red
    Write-Host "Files may be locked by XRM ToolBox. Close it completely and try again." -ForegroundColor Yellow
    exit 1
}
