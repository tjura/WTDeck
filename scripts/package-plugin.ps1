<#
.SYNOPSIS
Creates a distributable zip for the WTDeck Stream Dock plugin.

.DESCRIPTION
Runs package validation, creates `dist/` when needed, and writes
`dist/com.wtdeck.warthunder.sdPlugin.zip` from the current plugin source.
#>
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginName = "com.wtdeck.warthunder.sdPlugin"
$pluginRoot = Join-Path $repoRoot "plugin\$pluginName"
$distRoot = Join-Path $repoRoot "dist"
$packagePath = Join-Path $distRoot "$pluginName.zip"

& (Join-Path $PSScriptRoot "validate-plugin.ps1")

if (-not (Test-Path -LiteralPath $distRoot)) {
    New-Item -ItemType Directory -Path $distRoot | Out-Null
}

if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath
}

Compress-Archive -Path (Join-Path $pluginRoot "*") -DestinationPath $packagePath -Force
Write-Host "Created $packagePath"
