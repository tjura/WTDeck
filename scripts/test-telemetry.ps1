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
        [object] $RadarAltitudeMeters,
        [bool] $ActiveFlight = $true
    )

    if ($null -eq $IasKmh -or $null -eq $RadarAltitudeMeters) {
        if ($ActiveFlight) {
            return "NO DATA"
        }
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

function Get-AutoGearCandidate {
    param(
        [object] $GearPercent,
        [object] $IasKmh,
        [object] $TasKmh
    )

    if ($null -eq $GearPercent) {
        return "not ready: missing gear telemetry"
    }
    if ([double] $GearPercent -ge 95) {
        return "already down"
    }
    if ([double] $GearPercent -gt 5) {
        return "waiting: gear in transit"
    }

    $speedKmh = Get-MaxNumber -Values @($IasKmh, $TasKmh)
    if ($null -eq $speedKmh) {
        return "waiting: missing speed telemetry"
    }
    if ([double] $speedKmh -gt 350) {
        return "waiting for speed <= 350 km/h"
    }
    return "ready to extend"
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

function Test-NumberFieldPresent {
    param([object] $Value)

    if ($null -eq $Value) {
        return $false
    }
    if ($Value -is [string] -and $Value -eq "") {
        return $false
    }

    try {
        [void] ([double] $Value)
        return $true
    } catch {
        return $false
    }
}

function Convert-ToNumberOrNull {
    param([object] $Value)

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [string] -and $Value -eq "") {
        return $null
    }

    try {
        return [double] $Value
    } catch {
        return $null
    }
}

function Get-FirstNumber {
    param([object[]] $Values)

    foreach ($value in $Values) {
        $number = Convert-ToNumberOrNull -Value $value
        if ($null -ne $number) {
            return $number
        }
    }
    return $null
}

function Get-MaxNumber {
    param([object[]] $Values)

    $maximum = $null
    foreach ($value in $Values) {
        $number = Convert-ToNumberOrNull -Value $value
        if ($null -ne $number -and ($null -eq $maximum -or $number -gt $maximum)) {
            $maximum = $number
        }
    }
    return $maximum
}

function Get-SumMatchingNumberFields {
    param(
        [object] $Object,
        [string] $Pattern
    )

    if (-not $Object) {
        return $null
    }

    $sum = 0.0
    $found = $false
    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Name -notmatch $Pattern) {
            continue
        }
        $number = Convert-ToNumberOrNull -Value $property.Value
        if ($null -ne $number) {
            $sum += $number
            $found = $true
        }
    }
    if ($found) {
        return $sum
    }
    return $null
}

function Get-MaxMatchingNumberFields {
    param(
        [object] $Object,
        [string] $Pattern
    )

    if (-not $Object) {
        return $null
    }

    $maximum = $null
    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Name -notmatch $Pattern) {
            continue
        }
        $number = Convert-ToNumberOrNull -Value $property.Value
        if ($null -ne $number -and ($null -eq $maximum -or $number -gt $maximum)) {
            $maximum = $number
        }
    }
    return $maximum
}

function Test-InactiveAircraftSignature {
    param(
        [object] $State,
        [object] $Indicators
    )

    $fuelKg = Get-FirstNumber -Values @(
        (Get-Field -Object $Indicators -Name "fuel"),
        (Get-Field -Object $State -Name "Mfuel, kg"),
        (Get-SumMatchingNumberFields -Object $State -Pattern '^Mfuel \d+, kg$')
    )
    $initialFuelKg = Get-FirstNumber -Values @(
        (Get-Field -Object $State -Name "Mfuel0, kg"),
        (Get-SumMatchingNumberFields -Object $State -Pattern '^Mfuel0 \d+, kg$')
    )
    if ($null -eq $fuelKg -or $fuelKg -gt 0.05 -or $null -eq $initialFuelKg -or $initialFuelKg -le 0) {
        return $false
    }

    $indicatorSpeed = Convert-ToNumberOrNull -Value (Get-Field -Object $Indicators -Name "speed")
    if ($null -ne $indicatorSpeed) {
        $indicatorSpeed *= 3.6
    }
    $speedKmh = Get-MaxNumber -Values @(
        (Get-Field -Object $State -Name "IAS, km/h"),
        (Get-Field -Object $State -Name "TAS, km/h"),
        $indicatorSpeed
    )
    if ($null -ne $speedKmh -and $speedKmh -gt 5) {
        return $false
    }

    $verticalSpeedMps = Get-FirstNumber -Values @(
        (Get-Field -Object $State -Name "Vy, m/s"),
        (Get-Field -Object $Indicators -Name "vario")
    )
    if ($null -ne $verticalSpeedMps -and [math]::Abs($verticalSpeedMps) -gt 0.2) {
        return $false
    }

    $fuelConsume = Convert-ToNumberOrNull -Value (Get-Field -Object $Indicators -Name "fuel_consume")
    if ($null -ne $fuelConsume -and $fuelConsume -gt 0.05) {
        return $false
    }

    $engineOutput = Get-MaxNumber -Values @(
        (Get-Field -Object $Indicators -Name "rpm"),
        (Get-Field -Object $Indicators -Name "rpm1"),
        (Get-Field -Object $Indicators -Name "rpm2"),
        (Get-Field -Object $Indicators -Name "rpm3"),
        (Get-Field -Object $Indicators -Name "rpm_min"),
        (Get-Field -Object $Indicators -Name "rpm1_min"),
        (Get-Field -Object $Indicators -Name "rpm2_min"),
        (Get-Field -Object $Indicators -Name "rpm3_min"),
        (Get-MaxMatchingNumberFields -Object $State -Pattern '^RPM \d+$'),
        (Get-MaxMatchingNumberFields -Object $State -Pattern '^power \d+, hp$'),
        (Get-MaxMatchingNumberFields -Object $State -Pattern '^thrust \d+, kgs$'),
        (Get-MaxMatchingNumberFields -Object $State -Pattern '^efficiency \d+, %$')
    )
    return $null -eq $engineOutput -or $engineOutput -le 1
}

function Test-CoreFlightSample {
    param(
        [object] $State,
        [object] $Indicators
    )

    $fields = @(
        (Get-Field -Object $State -Name "IAS, km/h"),
        (Get-Field -Object $State -Name "TAS, km/h"),
        (Get-Field -Object $State -Name "H, m"),
        (Get-Field -Object $State -Name "Vy, m/s"),
        (Get-Field -Object $State -Name "Ny"),
        (Get-Field -Object $Indicators -Name "g_meter"),
        (Get-Field -Object $Indicators -Name "radio_altitude"),
        (Get-Field -Object $Indicators -Name "vario"),
        (Get-Field -Object $Indicators -Name "aviahorizon_pitch"),
        (Get-Field -Object $Indicators -Name "aviahorizon_pitch1"),
        (Get-Field -Object $Indicators -Name "aviahorizon_roll"),
        (Get-Field -Object $Indicators -Name "aviahorizon_roll1"),
        (Get-Field -Object $Indicators -Name "bank")
    )

    foreach ($field in $fields) {
        if (Test-NumberFieldPresent -Value $field) {
            return $true
        }
    }
    return $false
}

function Test-ActiveFlight {
    param(
        [object] $State,
        [object] $Indicators
    )

    $stateValid = Get-Field -Object $State -Name "valid"
    $indicatorsValid = Get-Field -Object $Indicators -Name "valid"
    $validFlight = Test-ValidFlight -State $State -Indicators $Indicators
    if ($stateValid -eq $false -or $indicatorsValid -eq $false) {
        return [pscustomobject]@{ Active = $false; Reason = "invalid telemetry" }
    }
    if (-not $validFlight) {
        return [pscustomobject]@{ Active = $false; Reason = "no telemetry" }
    }
    if ($stateValid -ne $true -and $indicatorsValid -ne $true) {
        return [pscustomobject]@{ Active = $false; Reason = "waiting for flight valid flag" }
    }

    $army = [string] (Get-Field -Object $Indicators -Name "army")
    $type = [string] (Get-Field -Object $Indicators -Name "type")
    if ($army.Trim().ToLowerInvariant() -ne "air" -or [string]::IsNullOrWhiteSpace($type)) {
        return [pscustomobject]@{ Active = $false; Reason = "no active aircraft" }
    }
    if (Test-InactiveAircraftSignature -State $State -Indicators $Indicators) {
        return [pscustomobject]@{ Active = $false; Reason = "inactive empty aircraft" }
    }
    if (-not (Test-CoreFlightSample -State $State -Indicators $Indicators)) {
        return [pscustomobject]@{ Active = $false; Reason = "no flight dynamics" }
    }
    return [pscustomobject]@{ Active = $true; Reason = "" }
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
$activeFlight = Test-ActiveFlight -State $stateResult.Body -Indicators $indicatorsResult.Body

Write-Host "Connected: yes"
Write-Host "/state: $(if ($stateResult.Ok) { "OK" } else { "FAILED" })"
Write-Host "/indicators: $(if ($indicatorsResult.Ok) { "OK" } else { "FAILED" })"
Write-Host "Valid flight: $(if ($null -eq $validFlight) { "unknown" } elseif ($validFlight) { "yes" } else { "no" })"
Write-Host "Active flight: $(if ($activeFlight.Active) { "yes" } else { "no" })"
if (-not $activeFlight.Active) {
    Write-Host "Inactive reason: $($activeFlight.Reason)"
}
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
$tasKmh = Get-Field -Object $stateResult.Body -Name "TAS, km/h"
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
Write-Host "state TAS, km/h: $(Format-FieldValue $tasKmh)"
Write-Host "throttle normalized, %: $(Format-FieldValue $throttlePercent)"
Write-Host "fuel normalized, kg: $(Format-FieldValue $fuelKg)"
Write-Host "fuel initial, kg: $(Format-FieldValue $initialFuelKg)"
Write-Host "fuel normalized, %: $(Format-FieldValue $fuelPercent)"
Write-Host "indicators fuel_consume: $(Format-FieldValue (Get-Field -Object $indicatorsResult.Body -Name "fuel_consume"))"
Write-Host "state Vy, m/s: $(Format-FieldValue $verticalSpeedMps)"
Write-Host "indicators radio_altitude normalized: $(Format-Meters $radioAltitudeMeters)"
Write-Host "optional drogue readiness: $(Get-DrogueReadiness -IasKmh $iasKmh -RadarAltitudeMeters $radioAltitudeMeters -ActiveFlight $activeFlight.Active)"
if ($activeFlight.Active) {
    Write-Host "auto gear extension: $(Get-AutoGearCandidate -GearPercent $gearPercent -IasKmh $iasKmh -TasKmh $tasKmh)"
    Write-Host "auto landing assist: $(Get-DrogueAutoAssistCandidate -GearPercent $gearPercent -IasKmh $iasKmh -RadarAltitudeMeters $radioAltitudeMeters -ThrottlePercent $throttlePercent -VerticalSpeedMps $verticalSpeedMps)"
} else {
    Write-Host "auto gear extension: inactive: $($activeFlight.Reason)"
    Write-Host "auto landing assist: inactive: $($activeFlight.Reason)"
}
Write-Host "optional drogue telemetry: deployed/released state is not exposed; Auto Landing skips chute deploy unless IAS <= 350 km/h and radar altitude <= 10 m"

if (-not $stateResult.Ok -or -not $indicatorsResult.Ok) {
    exit 1
}
