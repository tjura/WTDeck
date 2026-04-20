#!/usr/bin/env pwsh
# Dev helper: installs the WTDeck plugin + profile directly to the StreamDock
# user data directory, without running WTDeck.App.exe.
# Useful for fast iteration on plugin-only changes.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$pluginSrc = Join-Path $root "src\WTDeck.Plugin"
$pluginUuid = "com.wtdeck.streamdock"

$userDataRoot = Join-Path $env:APPDATA "HotSpot\StreamDock"
$pluginsDir = Join-Path $userDataRoot "plugins"
$targetDir = Join-Path $pluginsDir "$pluginUuid.sdPlugin"

if (-not (Test-Path $pluginsDir)) {
    Write-Host "StreamDock plugins directory not found: $pluginsDir" -ForegroundColor Red
    Write-Host "Is Stream Controller installed?"
    exit 1
}

# Stop Stream Controller if running
$running = Get-Process -Name "Stream Controller" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping Stream Controller..." -ForegroundColor Yellow
    $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Milliseconds 1500
    $stillRunning = Get-Process -Name "Stream Controller" -ErrorAction SilentlyContinue
    if ($stillRunning) {
        $stillRunning | Stop-Process -Force
    }
    Start-Sleep -Milliseconds 500
}

# Copy plugin files
if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Copy-Item (Join-Path $pluginSrc "manifest.json") $targetDir
Copy-Item (Join-Path $pluginSrc "plugin") (Join-Path $targetDir "plugin") -Recurse
Copy-Item (Join-Path $pluginSrc "assets") (Join-Path $targetDir "assets") -Recurse

Write-Host "Plugin installed to: $targetDir" -ForegroundColor Green
Write-Host ""

# Restart Stream Controller
$sc = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Stream Controller.exe" -ErrorAction SilentlyContinue
$exePath = if ($sc) { $sc.'(default)' } else { "C:\Program Files (x86)\Stream Controller\Stream Controller.exe" }

if (Test-Path $exePath) {
    Write-Host "Starting Stream Controller..." -ForegroundColor Yellow
    Start-Process $exePath
    Write-Host "Done." -ForegroundColor Green
} else {
    Write-Host "Stream Controller.exe not found; plugin installed but app not restarted." -ForegroundColor Yellow
}
