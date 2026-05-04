<#
.SYNOPSIS
Checks War Thunder localhost telemetry for the current cockpit actions.

.DESCRIPTION
Calls `/state` and `/indicators`, prints only the connection status and control
fields used by the plugin, and fails clearly when War Thunder telemetry is
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

    if ($null -eq $Value) {
        return "<missing>"
    }
    if ($Value -is [string] -and $Value -eq "") {
        return "<missing>"
    }
    return [string] $Value
}

function Convert-RadioAltitudeToMeters {
    param([object] $Value)

    if ($null -eq $Value -or $Value -eq "") {
        return $null
    }

    try {
        return [math]::Max(0, ([double] $Value) * 0.3048)
    } catch {
        return $null
    }
}

function Format-Meters {
    param([object] $Value)

    if ($null -eq $Value) {
        return "<missing>"
    }
    return "$([math]::Round([double] $Value, 1)) m"
}

function Convert-ToPercent {
    param([object] $Value)

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [string] -and $Value -eq "") {
        return $null
    }

    try {
        $number = [double] $Value
        if ($number -ge 0 -and $number -le 1) {
            return $number * 100
        }
        return $number
    } catch {
        return $null
    }
}

function Get-DrogueReadiness {
    param(
        [object] $IasKmh,
        [object] $RadarAltitudeMeters
    )

    if ($null -eq $IasKmh -or $null -eq $RadarAltitudeMeters) {
        return "NO FLIGHT"
    }

    if ([double] $RadarAltitudeMeters -gt 10) {
        return "AIR"
    }
    if ([double] $IasKmh -gt 350) {
        return "FAST"
    }
    return "READY"
}

function Get-DrogueAutoAssistCandidate {
    param(
        [object] $GearPercent,
        [object] $IasKmh,
        [object] $RadarAltitudeMeters,
        [object] $ThrottlePercent,
        [object] $VerticalSpeedMps
    )

    if ($null -eq $GearPercent -or $null -eq $IasKmh) {
        return "not ready: missing landing telemetry"
    }
    if ([double] $GearPercent -lt 95) {
        return "not ready: gear not down"
    }
    if ($null -eq $RadarAltitudeMeters) {
        $throttleIdle = $null -ne $ThrottlePercent -and [double] $ThrottlePercent -le 5
        if ($throttleIdle) {
            if ([double] $IasKmh -gt 260) {
                return "armed: no radio altitude, idle throttle, waiting for speed <= 260 km/h"
            }
        } elseif ([double] $IasKmh -gt 140) {
            return "armed: no radio altitude, waiting for idle throttle or speed <= 140 km/h"
        }
    } elseif ([double] $RadarAltitudeMeters -gt 3.5) {
        return "armed: waiting for touchdown"
    }
    if ([double] $IasKmh -gt 380) {
        return "not ready: touchdown speed too high"
    }
    if ($null -ne $VerticalSpeedMps -and [math]::Abs([double] $VerticalSpeedMps) -gt 1) {
        return "armed: waiting for stable touchdown"
    }
    if ($null -eq $RadarAltitudeMeters) {
        if ($null -ne $ThrottlePercent -and [double] $ThrottlePercent -le 5) {
            return "touchdown candidate: no radio altitude, gear down, idle throttle"
        }
        return "touchdown candidate: no radio altitude speed-only fallback"
    }
    return "touchdown candidate"
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
$gearPercent = Get-Field -Object $stateResult.Body -Name "gear, %"
Write-Host "state gear, %: $(Format-FieldValue $gearPercent)"
Write-Host "indicators gears_indicator: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears_indicator"))"
Write-Host "indicators gears: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears"))"
Write-Host "indicators gears_lamp: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "gears_lamp"))"
Write-Host "state flaps, %: $(Format-FieldValue (Get-Field -Object $stateResult.Body -Name "flaps, %"))"
Write-Host "indicators flaps_indicator: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "flaps_indicator"))"
Write-Host "indicators flaps: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "flaps"))"
Write-Host "state airbrake, %: $(Format-FieldValue (Get-Field -Object $stateResult.Body -Name "airbrake, %"))"
Write-Host "indicators airbrake_indicator: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "airbrake_indicator"))"
Write-Host "indicators airbrake_lever: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "airbrake_lever"))"
$iasKmh = Get-Field -Object $stateResult.Body -Name "IAS, km/h"
$throttlePercent = Convert-ToPercent (Get-Field -Object $stateResult.Body -Name "throttle 1, %")
if ($null -eq $throttlePercent) {
    $throttlePercent = Convert-ToPercent (Get-Field -Object $indicatorsResult.Body -Name "throttle")
}
$verticalSpeedMps = Get-Field -Object $stateResult.Body -Name "Vy, m/s"
$radioAltitudeMeters = Convert-RadioAltitudeToMeters (Get-Field -Object $indicatorsResult.Body -Name "radio_altitude")
$fuelKg = Get-Field -Object $indicatorsResult.Body -Name "fuel"
if ($null -eq $fuelKg -or $fuelKg -eq "") {
    $fuelKg = Get-Field -Object $stateResult.Body -Name "Mfuel, kg"
}
$initialFuelKg = Get-Field -Object $stateResult.Body -Name "Mfuel0, kg"
$fuelPercent = $null
if ($null -ne $fuelKg -and $fuelKg -ne "" -and $null -ne $initialFuelKg -and $initialFuelKg -ne "") {
    try {
        $fuelPercent = [math]::Round(([double] $fuelKg / [double] $initialFuelKg) * 100, 1)
    } catch {
        $fuelPercent = $null
    }
}
Write-Host "state IAS, km/h: $(Format-FieldValue $iasKmh)"
Write-Host "throttle normalized, %: $(Format-FieldValue $throttlePercent)"
Write-Host "fuel normalized, kg: $(Format-FieldValue $fuelKg)"
Write-Host "fuel initial, kg: $(Format-FieldValue $initialFuelKg)"
Write-Host "fuel normalized, %: $(Format-FieldValue $fuelPercent)"
Write-Host "indicators fuel_consume: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "fuel_consume"))"
Write-Host "state Vy, m/s: $(Format-FieldValue $verticalSpeedMps)"
Write-Host "indicators radio_altitude normalized: $(Format-Meters $radioAltitudeMeters)"
Write-Host "drogue chute state: $(Get-DrogueReadiness -IasKmh $iasKmh -RadarAltitudeMeters $radioAltitudeMeters)"
Write-Host "drogue auto landing assist: $(Get-DrogueAutoAssistCandidate -GearPercent $gearPercent -IasKmh $iasKmh -RadarAltitudeMeters $radioAltitudeMeters -ThrottlePercent $throttlePercent -VerticalSpeedMps $verticalSpeedMps)"
Write-Host "drogue chute telemetry: deployed/released state is not exposed; WTDeck gates command dispatch by IAS <= 350 km/h and radar altitude <= 10 m"

if (-not $stateResult.Ok -or -not $indicatorsResult.Ok) {
    exit 1
}
