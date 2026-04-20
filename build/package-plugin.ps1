#!/usr/bin/env pwsh
# Packages the WTDeck Stream Controller plugin as a .sdPlugin directory.
# The sync service inside WTDeck.App also installs the plugin at runtime;
# this script is for manual packaging/distribution.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$pluginSrc = Join-Path $root "src\WTDeck.Plugin"
$outputDir = Join-Path $root "publish"
$pluginUuid = "com.wtdeck.streamdock"
$packageDir = Join-Path $outputDir "$pluginUuid.sdPlugin"

Write-Host "Packaging WTDeck Stream Controller plugin..." -ForegroundColor Cyan

# Validate source
$manifestPath = Join-Path $pluginSrc "manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Host "manifest.json not found at $manifestPath" -ForegroundColor Red
    exit 1
}
$null = Get-Content $manifestPath -Raw | ConvertFrom-Json

# Clean package dir
if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

# Copy plugin files
Copy-Item $manifestPath $packageDir
Copy-Item (Join-Path $pluginSrc "plugin") (Join-Path $packageDir "plugin") -Recurse
Copy-Item (Join-Path $pluginSrc "assets") (Join-Path $packageDir "assets") -Recurse

Write-Host ""
Write-Host "Plugin packaged successfully!" -ForegroundColor Green
Write-Host "Output: $packageDir"
Write-Host ""
Write-Host "To install manually, copy '$pluginUuid.sdPlugin' to:"
Write-Host "  %APPDATA%\HotSpot\StreamDock\plugins\"
Write-Host "Then restart Stream Controller."
