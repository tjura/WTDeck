#!/usr/bin/env pwsh
# Publishes WTDeck.App as a self-contained single-file Windows executable.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\WTDeck.App\WTDeck.App.csproj"
$outputDir = Join-Path $root "publish"

Write-Host "Publishing WTDeck.App..." -ForegroundColor Cyan

dotnet publish $project `
    -c Release `
    -r win-x64 `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

$exe = Join-Path $outputDir "WTDeck.App.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length / 1MB
    Write-Host ""
    Write-Host "Published successfully!" -ForegroundColor Green
    Write-Host "Output: $exe"
    Write-Host "Size: $([math]::Round($size, 1)) MB"
} else {
    Write-Host "Publish completed but exe not found at expected path." -ForegroundColor Yellow
}
