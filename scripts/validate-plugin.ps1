<#
.SYNOPSIS
Validates the WTDeck Stream Dock plugin package.

.DESCRIPTION
Checks required plugin files, manifest action/config consistency, localization
coverage, and referenced image assets before local deploy or packaging.
#>
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = Join-Path $repoRoot "plugin\com.wtdeck.warthunder.sdPlugin"
$manifestPath = Join-Path $pluginRoot "manifest.json"
$actionsPath = Join-Path $pluginRoot "config\actions.json"
$defaultsPath = Join-Path $pluginRoot "config\defaults.json"
$localizationPath = Join-Path $pluginRoot "en.json"

$errors = New-Object System.Collections.Generic.List[string]

function Test-PluginFile {
    param(
        [string] $Path,
        [string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        $script:errors.Add("Missing $Label at $Path")
    }
}

function Test-ImageReference {
    param(
        [string] $Reference,
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        $script:errors.Add("Missing image reference for $Label")
        return
    }

    $basePath = Join-Path $pluginRoot $Reference
    $candidates = @("$basePath.svg", "$basePath.png", "$basePath.jpg", "$basePath.jpeg")
    if (-not ($candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)) {
        $script:errors.Add("Image reference '$Reference' for $Label does not resolve to svg/png/jpg")
    }
}

Test-PluginFile -Path $manifestPath -Label "manifest"
Test-PluginFile -Path $actionsPath -Label "action config"
Test-PluginFile -Path $defaultsPath -Label "default config"
Test-PluginFile -Path $localizationPath -Label "English localization"

if ($errors.Count -eq 0) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $actionsConfig = Get-Content -LiteralPath $actionsPath -Raw | ConvertFrom-Json
    $defaults = Get-Content -LiteralPath $defaultsPath -Raw | ConvertFrom-Json
    $localization = Get-Content -LiteralPath $localizationPath -Raw | ConvertFrom-Json

    Test-PluginFile -Path (Join-Path $pluginRoot $manifest.CodePath) -Label "CodePath"
    Test-PluginFile -Path (Join-Path $pluginRoot "property-inspector\index.html") -Label "property inspector"
    Test-ImageReference -Reference $manifest.Icon -Label "plugin icon"
    Test-ImageReference -Reference $manifest.CategoryIcon -Label "category icon"

    if (-not $defaults.telemetry.baseUrl) {
        $errors.Add("defaults.json is missing telemetry.baseUrl")
    }
    if (-not $defaults.telemetry.pollIntervalMs) {
        $errors.Add("defaults.json is missing telemetry.pollIntervalMs")
    }

    $configuredActionIds = @($actionsConfig.actions.PSObject.Properties.Name)
    $manifestActionIds = @($manifest.Actions.UUID)
    foreach ($action in $manifest.Actions) {
        if ($configuredActionIds -notcontains $action.UUID) {
            $errors.Add("Manifest action '$($action.UUID)' is missing from config/actions.json")
        }
        Test-ImageReference -Reference $action.Icon -Label "action $($action.UUID)"
        if ($action.PropertyInspectorPath) {
            Test-PluginFile -Path (Join-Path $pluginRoot $action.PropertyInspectorPath) -Label "PI for $($action.UUID)"
        }
    }

    foreach ($configuredActionId in $configuredActionIds) {
        if ($manifestActionIds -notcontains $configuredActionId) {
            $errors.Add("Configured action '$configuredActionId' is missing from manifest.json")
        }
    }

    $localizedActionIds = @($localization.PSObject.Properties.Name | Where-Object { $_ -like "com.wtdeck.warthunder.*" })
    foreach ($localizedActionId in $localizedActionIds) {
        if ($manifestActionIds -notcontains $localizedActionId) {
            $errors.Add("Localization action '$localizedActionId' is not present in manifest.json")
        }
    }

    foreach ($manifestActionId in $manifestActionIds) {
        if ($localizedActionIds -notcontains $manifestActionId) {
            $errors.Add("Manifest action '$manifestActionId' is missing from en.json")
        }
    }
}

if ($errors.Count -gt 0) {
    throw ($errors -join [Environment]::NewLine)
}

Write-Host "WTDeck plugin validation passed: $pluginRoot"
