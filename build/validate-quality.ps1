#!/usr/bin/env pwsh
# WTDeck quality validation gate.
# Runs all checks that must pass before a change is considered complete.
# Exit code 0 = all gates pass; non-zero = at least one gate failed.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$failed = $false

function Invoke-Gate {
    param([string]$Name, [scriptblock]$Command)
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    try {
        & $Command
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "Exit code $LASTEXITCODE"
        }
        Write-Host "PASS: $Name" -ForegroundColor Green
    } catch {
        Write-Host "FAIL: $Name - $_" -ForegroundColor Red
        $script:failed = $true
    }
}

Push-Location $root
try {
    # .NET gates
    Invoke-Gate "dotnet restore" { dotnet restore }
    Invoke-Gate "dotnet build -c Release -warnaserror" { dotnet build -c Release -warnaserror }
    Invoke-Gate "dotnet test -c Release --no-build" { dotnet test -c Release --no-build }
    Invoke-Gate "dotnet format --verify-no-changes" { dotnet format --verify-no-changes }

    # Plugin validation - manifest.json syntax check (vanilla JS plugin, no build step)
    Invoke-Gate "plugin manifest.json valid JSON" {
        $manifestPath = Join-Path $root "src\WTDeck.Plugin\manifest.json"
        if (-not (Test-Path $manifestPath)) {
            throw "manifest.json not found at $manifestPath"
        }
        $null = Get-Content $manifestPath -Raw | ConvertFrom-Json
        Write-Host "manifest.json parses successfully"
    }

    Invoke-Gate "plugin index.js exists" {
        $jsPath = Join-Path $root "src\WTDeck.Plugin\plugin\index.js"
        if (-not (Test-Path $jsPath)) {
            throw "plugin/index.js not found"
        }
        Write-Host "plugin/index.js present"
    }

    Invoke-Gate "plugin assets present" {
        $assetsDir = Join-Path $root "src\WTDeck.Plugin\assets"
        $required = @(
            "gear-retracted.svg", "gear-deployed.svg", "gear-deploying.svg",
            "gear-retracting.svg", "gear-damaged.svg", "gear-disabled.svg",
            "gear-unknown.svg", "plugin-icon.svg", "category-icon.svg"
        )
        foreach ($file in $required) {
            $path = Join-Path $assetsDir $file
            if (-not (Test-Path $path)) {
                throw "Missing asset: $file"
            }
        }
        Write-Host "$($required.Count) asset files present"
    }

    Write-Host ""
    if ($failed) {
        Write-Host "QUALITY GATE FAILED" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "ALL QUALITY GATES PASSED" -ForegroundColor Green
        exit 0
    }
} finally {
    Pop-Location
}
