<#
.SYNOPSIS
Checks War Thunder localhost telemetry for the current Landing Gear action.

.DESCRIPTION
Calls `/state` and `/indicators`, prints only the connection status and landing
gear fields used by the plugin, and fails clearly when War Thunder telemetry is
unavailable.
#>
param(
    [string] $BaseUrl
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot ".env"

function Import-DotEnv {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#") -or -not $trimmed.Contains("=")) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf("=")
        $name = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        if ($value.Length -ge 2) {
            $first = $value[0]
            $last = $value[$value.Length - 1]
            if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }

        if ($name) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

function Invoke-TelemetryEndpoint {
    param(
        [string] $Url,
        [string] $Name
    )

    try {
        $body = Invoke-RestMethod -Uri $Url -Method Get -TimeoutSec 2
        return [pscustomobject]@{
            Name = $Name
            Ok = $true
            Body = $body
            Error = $null
        }
    } catch {
        return [pscustomobject]@{
            Name = $Name
            Ok = $false
            Body = $null
            Error = $_.Exception.Message
        }
    }
}

function Get-Field {
    param(
        [object] $Object,
        [string] $Name
    )

    if (-not $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($property) {
        return $property.Value
    }
    return $null
}

function Format-FieldValue {
    param([object] $Value)

    if ($null -eq $Value -or $Value -eq "") {
        return "<missing>"
    }
    return [string] $Value
}

function Test-ValidFlight {
    param(
        [object] $State,
        [object] $Indicators
    )

    $stateValid = Get-Field -Object $State -Name "valid"
    $indicatorsValid = Get-Field -Object $Indicators -Name "valid"
    if ($stateValid -eq $false -or $indicatorsValid -eq $false) {
        return $false
    }
    if ($stateValid -eq $true -or $indicatorsValid -eq $true) {
        return $true
    }
    return $null
}

Import-DotEnv -Path $envPath

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = [Environment]::GetEnvironmentVariable("WT_TELEMETRY_URL", "Process")
}
if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = "http://127.0.0.1:8111"
}

$BaseUrl = $BaseUrl.TrimEnd("/")
$stateResult = Invoke-TelemetryEndpoint -Url "$BaseUrl/state" -Name "/state"
$indicatorsResult = Invoke-TelemetryEndpoint -Url "$BaseUrl/indicators" -Name "/indicators"

if (-not $stateResult.Ok -and -not $indicatorsResult.Ok) {
    Write-Error "War Thunder telemetry is unavailable at $BaseUrl. Start War Thunder, enter a flight or test flight, and retry. /state: $($stateResult.Error); /indicators: $($indicatorsResult.Error)"
    exit 1
}

$validFlight = Test-ValidFlight -State $stateResult.Body -Indicators $indicatorsResult.Body

Write-Host "Connected: yes"
Write-Host "/state: $(if ($stateResult.Ok) { "OK" } else { "FAILED" })"
Write-Host "/indicators: $(if ($indicatorsResult.Ok) { "OK" } else { "FAILED" })"
Write-Host "Valid flight: $(if ($null -eq $validFlight) { "unknown" } elseif ($validFlight) { "yes" } else { "no" })"
Write-Host "state gear, %: $(Format-FieldValue (Get-Field -Object $stateResult.Body -Name "gear, %"))"
Write-Host "indicators gears_indicator: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears_indicator"))"
Write-Host "indicators gears: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears"))"
Write-Host "indicators gears_lamp: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears_lamp"))"

if (-not $stateResult.Ok -or -not $indicatorsResult.Ok) {
    exit 1
}
