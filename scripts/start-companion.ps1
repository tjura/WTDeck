<#
.SYNOPSIS
Starts, stops, or restarts the local WTDeck key sender companion.

.DESCRIPTION
The companion listens on localhost and accepts WTDeck command intents from the
Stream Dock plugin. It translates key down, key up, or tap phases into Win32
SendInput scan-code keyboard events for the focused game window.
#>
param(
    [switch] $Worker,
    [switch] $Stop,
    [switch] $Restart,
    [int] $Port = 34911,
    [string] $PidPath,
    [string] $LogPath
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

function Get-ConfigValue {
    param(
        [string] $Name,
        [string] $DefaultValue
    )

    $value = [Environment]::GetEnvironmentVariable($Name, "Process")
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $DefaultValue
    }
    return $value
}

function Write-CompanionLog {
    param([string] $Message)

    if ([string]::IsNullOrWhiteSpace($script:CompanionLogPath)) {
        return
    }

    $line = (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff") + " " + $Message
    Add-Content -LiteralPath $script:CompanionLogPath -Value $line
}

function Test-CompanionHealth {
    param([int] $HealthPort)

    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:$HealthPort/health" -UseBasicParsing -TimeoutSec 1
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Stop-ExistingCompanion {
    param([string] $ExistingPidPath)

    if (-not (Test-Path -LiteralPath $ExistingPidPath)) {
        return
    }

    $rawPid = (Get-Content -LiteralPath $ExistingPidPath -Raw).Trim()
    if (-not ($rawPid -match '^\d+$')) {
        Remove-Item -LiteralPath $ExistingPidPath -Force -ErrorAction SilentlyContinue
        return
    }

    $process = Get-Process -Id ([int] $rawPid) -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 5 -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $ExistingPidPath -Force -ErrorAction SilentlyContinue
}

function Quote-Argument {
    param([string] $Value)
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Resolve-KeySpec {
    param([object] $Command)

    if ($Command -and $Command.PSObject.Properties["scanCodes"]) {
        $scanDescriptors = @()
        foreach ($rawCode in @($Command.scanCodes)) {
            if ($null -eq $rawCode -or [string]::IsNullOrWhiteSpace([string] $rawCode)) {
                continue
            }
            $scanCode = [int] $rawCode
            if ($scanCode -lt 1 -or $scanCode -gt 255) {
                throw "Unsupported scan code."
            }
            $label = Convert-DikCodeToLabel -DikCode $scanCode
            if ([string]::IsNullOrWhiteSpace($label)) {
                $label = "DIK$scanCode"
            }
            $role = if (Test-ModifierDikCode -DikCode $scanCode) { "modifier" } else { "main" }
            $vkey = Resolve-KeyCodeOrZero -Label $label
            $scanDescriptors += New-KeyDescriptor -Label $label -VKeyCode $vkey -ScanCode $scanCode -Role $role
        }
        if (@($scanDescriptors).Count -gt 0) {
            $main = $scanDescriptors | Where-Object { $_.Role -ne "modifier" } | Select-Object -Last 1
            if (-not $main) {
                throw "Scan-code binding must include a main key."
            }
            $resolvedLabel = if ($Command.PSObject.Properties["hotkeyLabel"]) {
                ([string] $Command.hotkeyLabel).Trim()
            } else {
                (@($scanDescriptors | ForEach-Object { $_.Label }) -join "+")
            }
            return [pscustomobject]@{
                Label = $resolvedLabel
                Modifiers = @($scanDescriptors | Where-Object { $_.Role -eq "modifier" })
                Main = $main
                Keys = @($scanDescriptors)
            }
        }
    }

    if ($Command -and $Command.PSObject.Properties["vKeyCode"]) {
        $vkey = [int] $Command.vKeyCode
        if ($vkey -lt 1 -or $vkey -gt 255) {
            throw "Unsupported hotkey label."
        }
        $main = New-KeyDescriptor -Label "VK$vkey" -VKeyCode $vkey -ScanCode 0 -Role "main"
        return [pscustomobject]@{
            Label = "VK$vkey"
            Modifiers = @()
            Main = $main
            Keys = @($main)
        }
    }

    $label = ""
    if ($Command -and $Command.PSObject.Properties["hotkeyLabel"]) {
        $label = ([string] $Command.hotkeyLabel).Trim()
    }

    if ([string]::IsNullOrWhiteSpace($label) -and $Command -and $Command.PSObject.Properties["intent"]) {
        switch ([string] $Command.intent) {
            "landing-gear-toggle" { $label = "G"; break }
            "airbrake-toggle" { $label = "H"; break }
            "flaps-up" { $label = "PageUp"; break }
            "flaps-down" { $label = "PageDown"; break }
            "drogue-chute-deploy" { $label = "Shift+G"; break }
        }
    }

    if ([string]::IsNullOrWhiteSpace($label)) {
        throw "Unsupported hotkey label."
    }

    $modifiers = @()
    $main = $null
    foreach ($part in ($label -split "\+")) {
        $trimmed = $part.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $modifierCode = Resolve-ModifierCode -Label $trimmed
        if ($null -ne $modifierCode) {
            $modifiers += New-KeyDescriptor -Label $trimmed.ToUpperInvariant() -VKeyCode $modifierCode -ScanCode 0 -Role "modifier"
            continue
        }

        if ($null -ne $main) {
            throw "Hotkey label must contain only one main key."
        }
        $main = New-KeyDescriptor -Label $trimmed.ToUpperInvariant() -VKeyCode (Resolve-KeyCode -Label $trimmed) -ScanCode 0 -Role "main"
    }

    if ($null -eq $main) {
        throw "Hotkey label must include a main key."
    }

    $keys = @($modifiers) + @($main)
    return [pscustomobject]@{
        Label = $label
        Modifiers = @($modifiers)
        Main = $main
        Keys = @($keys)
    }
}

function New-KeyDescriptor {
    param(
        [string] $Label,
        [int] $VKeyCode,
        [int] $ScanCode,
        [string] $Role
    )

    if (($VKeyCode -lt 1 -or $VKeyCode -gt 255) -and ($ScanCode -lt 1 -or $ScanCode -gt 255)) {
        throw "Unsupported hotkey label."
    }

    return [pscustomobject]@{
        Label = $Label
        VKeyCode = [uint16] $VKeyCode
        ScanCode = [uint16] $ScanCode
        Role = $Role
    }
}

function Test-ModifierDikCode {
    param([int] $DikCode)

    return $DikCode -eq 29 -or $DikCode -eq 42 -or $DikCode -eq 54 -or
        $DikCode -eq 56 -or $DikCode -eq 157 -or $DikCode -eq 184
}

function Resolve-KeyCodeOrZero {
    param([string] $Label)

    try {
        return Resolve-KeyCode -Label $Label
    } catch {
        return 0
    }
}

function Resolve-ModifierCode {
    param([string] $Label)

    switch -Regex ($Label.Trim().ToUpperInvariant()) {
        "^(SHIFT|SHFT)$" { return 16 }
        "^(CTRL|CONTROL|CTL)$" { return 17 }
        "^(ALT|MENU|OPTION)$" { return 18 }
        default { return $null }
    }
}

function Resolve-KeyCode {
    param([string] $Label)

    $normalized = $Label.Trim().ToUpperInvariant()
    if ($normalized.Length -eq 1) {
        if ($normalized -match '^[A-Z0-9]$') {
            return [int][char] $normalized
        }
        switch ($normalized) {
            ";" { return 186 }
            "=" { return 187 }
            "," { return 188 }
            "-" { return 189 }
            "." { return 190 }
            "/" { return 191 }
            '`' { return 192 }
            "[" { return 219 }
            "\" { return 220 }
            "]" { return 221 }
            "'" { return 222 }
        }
    }

    switch -Regex ($normalized) {
        "^(SPACE|SPACEBAR)$" { return 32 }
        "^(ESC|ESCAPE)$" { return 27 }
        "^ENTER$" { return 13 }
        "^TAB$" { return 9 }
        "^(BACKSPACE|BKSP)$" { return 8 }
        "^(DELETE|DEL)$" { return 46 }
        "^(INSERT|INS)$" { return 45 }
        "^HOME$" { return 36 }
        "^END$" { return 35 }
        "^(PAGEUP|PGUP)$" { return 33 }
        "^(PAGEDOWN|PGDN)$" { return 34 }
        "^UP$" { return 38 }
        "^DOWN$" { return 40 }
        "^LEFT$" { return 37 }
        "^RIGHT$" { return 39 }
        "^NUMPAD([0-9])$" {
            return 96 + [int] $Matches[1]
        }
        "^NUMPAD\*$" { return 106 }
        "^NUMPAD\+$" { return 107 }
        "^NUMPAD-$" { return 109 }
        "^NUMPAD\.$" { return 110 }
        "^NUMPAD/$" { return 111 }
        "^F([1-9]|1[0-9]|2[0-4])$" {
            return 111 + [int] $Matches[1]
        }
        default {
            throw "Unsupported hotkey label."
        }
    }
}

function Send-KeySpec {
    param(
        [object] $KeySpec,
        [string] $Phase
    )

    switch ($Phase) {
        "down" {
            foreach ($key in @($KeySpec.Keys)) {
                if (-not (Send-KeyDown -Key $key)) {
                    return $false
                }
            }
            return $true
        }
        "up" {
            for ($index = @($KeySpec.Keys).Count - 1; $index -ge 0; $index -= 1) {
                if (-not (Send-KeyUp -Key @($KeySpec.Keys)[$index])) {
                    return $false
                }
            }
            return $true
        }
        default {
            foreach ($key in @($KeySpec.Keys)) {
                if (-not (Send-KeyDown -Key $key)) {
                    return $false
                }
            }
            for ($index = @($KeySpec.Keys).Count - 1; $index -ge 0; $index -= 1) {
                if (-not (Send-KeyUp -Key @($KeySpec.Keys)[$index])) {
                    return $false
                }
            }
            return $true
        }
    }
}

function Send-KeyDown {
    param([object] $Key)

    if ($Key.PSObject.Properties["ScanCode"] -and [int] $Key.ScanCode -gt 0) {
        return [WTDeckInput.NativeMethods]::SendScanCodeDown($Key.ScanCode)
    }
    return [WTDeckInput.NativeMethods]::SendVirtualKeyDown($Key.VKeyCode)
}

function Send-KeyUp {
    param([object] $Key)

    if ($Key.PSObject.Properties["ScanCode"] -and [int] $Key.ScanCode -gt 0) {
        return [WTDeckInput.NativeMethods]::SendScanCodeUp($Key.ScanCode)
    }
    return [WTDeckInput.NativeMethods]::SendVirtualKeyUp($Key.VKeyCode)
}

function Resolve-CommandPhase {
    param([object] $Command)

    $phase = "tap"
    if ($Command -and $Command.PSObject.Properties["phase"]) {
        $phase = ([string] $Command.phase).Trim().ToLowerInvariant()
    } elseif ($Command -and $Command.PSObject.Properties["event"]) {
        $phase = ([string] $Command.event).Trim().ToLowerInvariant()
    }

    if ([string]::IsNullOrWhiteSpace($phase)) {
        return "tap"
    }

    switch ($phase) {
        "down" { return "down" }
        "keydown" { return "down" }
        "key-down" { return "down" }
        "press" { return "down" }
        "up" { return "up" }
        "keyup" { return "up" }
        "key-up" { return "up" }
        "release" { return "up" }
        "tap" { return "tap" }
        "keypress" { return "tap" }
        "key-press" { return "tap" }
        default { throw "Unsupported command phase '$phase'." }
    }
}

function Split-RequestTarget {
    param([string] $Target)

    $path = $Target
    $queryString = ""
    $queryIndex = $Target.IndexOf("?")
    if ($queryIndex -ge 0) {
        $path = $Target.Substring(0, $queryIndex)
        $queryString = $Target.Substring($queryIndex + 1)
    }

    return [pscustomobject]@{
        Path = $path
        Query = ConvertFrom-QueryString -QueryString $queryString
    }
}

function ConvertFrom-QueryString {
    param([string] $QueryString)

    $query = @{}
    if ([string]::IsNullOrWhiteSpace($QueryString)) {
        return $query
    }

    foreach ($pair in ($QueryString -split "&")) {
        if ([string]::IsNullOrWhiteSpace($pair)) {
            continue
        }

        $separatorIndex = $pair.IndexOf("=")
        if ($separatorIndex -ge 0) {
            $name = $pair.Substring(0, $separatorIndex)
            $value = $pair.Substring($separatorIndex + 1)
        } else {
            $name = $pair
            $value = ""
        }

        $name = [uri]::UnescapeDataString($name.Replace("+", " "))
        $value = [uri]::UnescapeDataString($value.Replace("+", " "))
        if ($name) {
            $query[$name] = $value
        }
    }

    return $query
}

function Get-ActionBindingMetadata {
    param([string] $ActionUuid)

    if ([string]::IsNullOrWhiteSpace($ActionUuid)) {
        throw "Missing actionUuid."
    }

    $actionsPath = Join-Path $repoRoot "plugin\com.wtdeck.warthunder.sdPlugin\config\actions.json"
    if (-not (Test-Path -LiteralPath $actionsPath)) {
        throw "WTDeck action config was not found."
    }

    $actionsConfig = Get-Content -LiteralPath $actionsPath -Raw | ConvertFrom-Json
    $actionProperty = $actionsConfig.actions.PSObject.Properties |
        Where-Object { $_.Name -eq $ActionUuid } |
        Select-Object -First 1
    if (-not $actionProperty) {
        throw "Unknown actionUuid '$ActionUuid'."
    }

    $command = $actionProperty.Value.command
    $controlId = ""
    $defaultHotkeyLabel = ""
    $intent = ""
    if ($command) {
        if ($command.PSObject.Properties["warThunderControlId"]) {
            $controlId = ([string] $command.warThunderControlId).Trim()
        }
        if ($command.PSObject.Properties["defaultHotkeyLabel"]) {
            $defaultHotkeyLabel = ([string] $command.defaultHotkeyLabel).Trim()
        }
        if ($command.PSObject.Properties["intent"]) {
            $intent = ([string] $command.intent).Trim()
        }
    }

    return [pscustomobject]@{
        ActionUuid = $ActionUuid
        ControlId = $controlId
        DefaultHotkeyLabel = $defaultHotkeyLabel
        Intent = $intent
    }
}

function Get-WarThunderSaveRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    $configuredRoot = Get-ConfigValue -Name "WT_WARTHUNDER_SAVES_DIR" -DefaultValue ""
    if (-not [string]::IsNullOrWhiteSpace($configuredRoot)) {
        $roots.Add($configuredRoot)
    }

    $documents = [Environment]::GetFolderPath("MyDocuments")
    if (-not [string]::IsNullOrWhiteSpace($documents)) {
        $roots.Add((Join-Path $documents "My Games\WarThunder\Saves"))
    }

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $roots.Add((Join-Path $env:USERPROFILE "Documents\My Games\WarThunder\Saves"))
        $roots.Add((Join-Path $env:USERPROFILE "OneDrive\Documents\My Games\WarThunder\Saves"))
        $roots.Add((Join-Path $env:USERPROFILE "OneDrive\Dokumenty\My Games\WarThunder\Saves"))
    }

    return @($roots | Select-Object -Unique)
}

function Resolve-WarThunderMachinePath {
    foreach ($root in Get-WarThunderSaveRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $lastMachinePath = Join-Path $root "last\production\machine.blk"
        if (Test-Path -LiteralPath $lastMachinePath) {
            return $lastMachinePath
        }

        $profileMachine = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName "production\machine.blk" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            ForEach-Object { Get-Item -LiteralPath $_ } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($profileMachine) {
            return $profileMachine.FullName
        }
    }

    return ""
}

function Get-ControlBindingFromBlk {
    param(
        [string] $Path,
        [string] $ControlId
    )

    $result = [pscustomobject]@{
        BlockFound = $false
        KeyboardCodes = @()
    }

    if ([string]::IsNullOrWhiteSpace($Path) -or
        [string]::IsNullOrWhiteSpace($ControlId) -or
        -not (Test-Path -LiteralPath $Path)) {
        return $result
    }

    $content = Get-Content -LiteralPath $Path -Raw
    $pattern = "(?ms)^\s*" + [regex]::Escape($ControlId) + "\s*\{\s*(.*?)^\s*\}"
    foreach ($match in [regex]::Matches($content, $pattern)) {
        $result.BlockFound = $true
        $codeMatches = [regex]::Matches($match.Groups[1].Value, "keyboardKey:i=(-?\d+)")
        if ($codeMatches.Count -gt 0) {
            $result.KeyboardCodes = @($codeMatches | ForEach-Object { [int] $_.Groups[1].Value })
            return $result
        }
    }

    return $result
}

function Convert-DikCodesToHotkeyLabel {
    param([int[]] $DikCodes)

    $modifiers = New-Object System.Collections.Generic.List[string]
    $mainKeys = New-Object System.Collections.Generic.List[string]
    foreach ($code in @($DikCodes)) {
        $label = Convert-DikCodeToLabel -DikCode $code
        if ([string]::IsNullOrWhiteSpace($label)) {
            return $null
        }

        if ($label -eq "Shift" -or $label -eq "Ctrl" -or $label -eq "Alt") {
            if (-not $modifiers.Contains($label)) {
                $modifiers.Add($label)
            }
            continue
        }

        $mainKeys.Add($label)
    }

    if ($mainKeys.Count -ne 1) {
        return $null
    }

    return (@($modifiers) + @($mainKeys[0])) -join "+"
}

function Convert-DikCodeToLabel {
    param([int] $DikCode)

    switch ($DikCode) {
        1 { return "Esc" }
        2 { return "1" }
        3 { return "2" }
        4 { return "3" }
        5 { return "4" }
        6 { return "5" }
        7 { return "6" }
        8 { return "7" }
        9 { return "8" }
        10 { return "9" }
        11 { return "0" }
        12 { return "-" }
        13 { return "=" }
        14 { return "Backspace" }
        15 { return "Tab" }
        16 { return "Q" }
        17 { return "W" }
        18 { return "E" }
        19 { return "R" }
        20 { return "T" }
        21 { return "Y" }
        22 { return "U" }
        23 { return "I" }
        24 { return "O" }
        25 { return "P" }
        26 { return "[" }
        27 { return "]" }
        28 { return "Enter" }
        29 { return "Ctrl" }
        30 { return "A" }
        31 { return "S" }
        32 { return "D" }
        33 { return "F" }
        34 { return "G" }
        35 { return "H" }
        36 { return "J" }
        37 { return "K" }
        38 { return "L" }
        39 { return ";" }
        40 { return "'" }
        41 { return '`' }
        42 { return "Shift" }
        43 { return "\" }
        44 { return "Z" }
        45 { return "X" }
        46 { return "C" }
        47 { return "V" }
        48 { return "B" }
        49 { return "N" }
        50 { return "M" }
        51 { return "," }
        52 { return "." }
        53 { return "/" }
        54 { return "Shift" }
        55 { return "Numpad*" }
        56 { return "Alt" }
        57 { return "Space" }
        59 { return "F1" }
        60 { return "F2" }
        61 { return "F3" }
        62 { return "F4" }
        63 { return "F5" }
        64 { return "F6" }
        65 { return "F7" }
        66 { return "F8" }
        67 { return "F9" }
        68 { return "F10" }
        71 { return "Numpad7" }
        72 { return "Numpad8" }
        73 { return "Numpad9" }
        74 { return "Numpad-" }
        75 { return "Numpad4" }
        76 { return "Numpad5" }
        77 { return "Numpad6" }
        78 { return "Numpad+" }
        79 { return "Numpad1" }
        80 { return "Numpad2" }
        81 { return "Numpad3" }
        82 { return "Numpad0" }
        83 { return "Numpad." }
        87 { return "F11" }
        88 { return "F12" }
        156 { return "Enter" }
        157 { return "Ctrl" }
        181 { return "Numpad/" }
        184 { return "Alt" }
        199 { return "Home" }
        200 { return "Up" }
        201 { return "PageUp" }
        203 { return "Left" }
        205 { return "Right" }
        207 { return "End" }
        208 { return "Down" }
        209 { return "PageDown" }
        210 { return "Insert" }
        211 { return "Delete" }
        default { return $null }
    }
}

function Get-ControlIdCandidates {
    param(
        [string] $ControlId,
        [string] $ControlIds,
        [string] $DefaultControlId
    )

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($raw in @($ControlIds -split ",") + @($ControlId, $DefaultControlId)) {
        $id = ([string] $raw).Trim()
        if ([string]::IsNullOrWhiteSpace($id) -or $ids.Contains($id)) {
            continue
        }
        $ids.Add($id)
        if ($id -eq "ID_WHEEL_BRAKE") {
            foreach ($alias in @("brake_left_rangeMax", "brake_right_rangeMax")) {
                if (-not $ids.Contains($alias)) {
                    $ids.Add($alias)
                }
            }
        }
    }

    return @($ids)
}

function Resolve-WarThunderBinding {
    param(
        [string] $ActionUuid,
        [string] $ControlId,
        [string] $ControlIds,
        [string] $DefaultHotkeyLabel,
        [string] $Intent
    )

    $metadata = Get-ActionBindingMetadata -ActionUuid $ActionUuid
    $controlIdCandidates = Get-ControlIdCandidates `
        -ControlId $ControlId `
        -ControlIds $ControlIds `
        -DefaultControlId $metadata.ControlId
    if (@($controlIdCandidates).Count -gt 0) {
        $metadata.ControlId = @($controlIdCandidates) -join ","
    }
    if (-not [string]::IsNullOrWhiteSpace($DefaultHotkeyLabel)) {
        $metadata.DefaultHotkeyLabel = $DefaultHotkeyLabel.Trim()
    }
    if (-not [string]::IsNullOrWhiteSpace($Intent)) {
        $metadata.Intent = $Intent.Trim()
    }
    if ([string]::IsNullOrWhiteSpace($metadata.ControlId) -and
        [string]::IsNullOrWhiteSpace($metadata.DefaultHotkeyLabel)) {
        return [pscustomobject]@{
            ok = $false
            actionUuid = $metadata.ActionUuid
            error = "no binding needed"
        }
    }

    $machinePath = Resolve-WarThunderMachinePath
    if (@($controlIdCandidates).Count -gt 0 -and
        -not [string]::IsNullOrWhiteSpace($machinePath)) {
        $foundKeyboardlessControlId = ""
        foreach ($candidateControlId in @($controlIdCandidates)) {
            $binding = Get-ControlBindingFromBlk -Path $machinePath -ControlId $candidateControlId
            if (-not $binding.BlockFound) {
                continue
            }
            if (@($binding.KeyboardCodes).Count -eq 0) {
                $foundKeyboardlessControlId = $candidateControlId
                continue
            }

            $hotkeyLabel = Convert-DikCodesToHotkeyLabel -DikCodes $binding.KeyboardCodes
            if ([string]::IsNullOrWhiteSpace($hotkeyLabel)) {
                return [pscustomobject]@{
                    ok = $false
                    actionUuid = $metadata.ActionUuid
                    controlId = $candidateControlId
                    source = "war-thunder-machine"
                    path = $machinePath
                    error = "unsupported keyboard binding"
                }
            }

            return [pscustomobject]@{
                ok = $true
                actionUuid = $metadata.ActionUuid
                controlId = $candidateControlId
                hotkeyLabel = $hotkeyLabel
                scanCodes = @($binding.KeyboardCodes)
                source = "war-thunder-machine"
                path = $machinePath
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($foundKeyboardlessControlId)) {
            return [pscustomobject]@{
                ok = $false
                actionUuid = $metadata.ActionUuid
                controlId = $foundKeyboardlessControlId
                source = "war-thunder-machine"
                path = $machinePath
                error = "no keyboard binding found"
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($metadata.DefaultHotkeyLabel)) {
        return [pscustomobject]@{
            ok = $true
            actionUuid = $metadata.ActionUuid
            controlId = $metadata.ControlId
            hotkeyLabel = $metadata.DefaultHotkeyLabel
            source = "wtdeck-default"
            path = $machinePath
        }
    }

    return [pscustomobject]@{
        ok = $false
        actionUuid = $metadata.ActionUuid
        controlId = $metadata.ControlId
        path = $machinePath
        error = "no keyboard binding found"
    }
}

function Initialize-SendInput {
    if ("WTDeckInput.NativeMethods" -as [type]) {
        return
    }

    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace WTDeckInput {
    public static class NativeMethods {
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT {
            public UInt32 type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT {
            public Int32 dx;
            public Int32 dy;
            public UInt32 mouseData;
            public UInt32 dwFlags;
            public UInt32 time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT {
            public UInt16 wVk;
            public UInt16 wScan;
            public UInt32 dwFlags;
            public UInt32 time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT {
            public UInt32 uMsg;
            public UInt16 wParamL;
            public UInt16 wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern UInt32 SendInput(UInt32 nInputs, INPUT[] pInputs, Int32 cbSize);

        [DllImport("user32.dll")]
        public static extern UInt32 MapVirtualKey(UInt32 uCode, UInt32 uMapType);

        public const UInt32 INPUT_KEYBOARD = 1;
        public const UInt32 KEYEVENTF_KEYUP = 0x0002;
        public const UInt32 KEYEVENTF_SCANCODE = 0x0008;

        private static INPUT KeyboardInput(UInt16 vkey, bool keyUp) {
            UInt16 scan = (UInt16)MapVirtualKey(vkey, 0);
            return ScanCodeInput(scan, keyUp);
        }

        private static INPUT ScanCodeInput(UInt16 scan, bool keyUp) {
            INPUT input = new INPUT();

            input.type = INPUT_KEYBOARD;
            input.U.ki.wVk = 0;
            input.U.ki.wScan = scan;
            input.U.ki.dwFlags = KEYEVENTF_SCANCODE;
            if (keyUp) {
                input.U.ki.dwFlags |= KEYEVENTF_KEYUP;
            }
            input.U.ki.time = 0;
            input.U.ki.dwExtraInfo = UIntPtr.Zero;

            return input;
        }

        private static bool SendKeyboardInputs(INPUT[] inputs) {
            return SendInput((UInt32)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == inputs.Length;
        }

        public static bool SendVirtualKeyDown(UInt16 vkey) {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = KeyboardInput(vkey, false);
            return SendKeyboardInputs(inputs);
        }

        public static bool SendVirtualKeyUp(UInt16 vkey) {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = KeyboardInput(vkey, true);
            return SendKeyboardInputs(inputs);
        }

        public static bool SendScanCodeDown(UInt16 scan) {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = ScanCodeInput(scan, false);
            return SendKeyboardInputs(inputs);
        }

        public static bool SendScanCodeUp(UInt16 scan) {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = ScanCodeInput(scan, true);
            return SendKeyboardInputs(inputs);
        }

        public static bool SendVirtualKeyTap(UInt16 vkey) {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = KeyboardInput(vkey, false);
            inputs[1] = KeyboardInput(vkey, true);
            return SendKeyboardInputs(inputs);
        }

        public static bool SendVirtualKey(UInt16 vkey) {
            return SendVirtualKeyTap(vkey);
        }

        public static int LastError() {
            return Marshal.GetLastWin32Error();
        }
    }
}
"@
}

function Send-HttpResponse {
    param(
        [System.Net.Sockets.NetworkStream] $Stream,
        [int] $StatusCode,
        [string] $Reason,
        [string] $Body,
        [string] $ContentType = "application/json"
    )

    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)
    $headers = @(
        "HTTP/1.1 $StatusCode $Reason",
        "Content-Type: $ContentType; charset=utf-8",
        "Content-Length: $($bodyBytes.Length)",
        "Connection: close",
        "Access-Control-Allow-Origin: *",
        "Access-Control-Allow-Headers: content-type",
        "Access-Control-Allow-Methods: GET, POST, OPTIONS",
        "",
        ""
    ) -join "`r`n"

    $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
    $Stream.Write($headerBytes, 0, $headerBytes.Length)
    if ($bodyBytes.Length -gt 0) {
        $Stream.Write($bodyBytes, 0, $bodyBytes.Length)
    }
}

function Send-JsonResponse {
    param(
        [System.Net.Sockets.NetworkStream] $Stream,
        [int] $StatusCode,
        [string] $Reason,
        [object] $Value
    )

    $body = $Value | ConvertTo-Json -Compress -Depth 8
    Send-HttpResponse -Stream $Stream -StatusCode $StatusCode -Reason $Reason -Body $body
}

function Handle-Client {
    param([System.Net.Sockets.TcpClient] $Client)

    try {
        $stream = $Client.GetStream()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
        $requestLine = $reader.ReadLine()
        if ([string]::IsNullOrWhiteSpace($requestLine)) {
            return
        }

        $parts = $requestLine.Split(" ")
        $method = $parts[0]
        $requestTarget = Split-RequestTarget -Target $parts[1]
        $path = $requestTarget.Path
        $query = $requestTarget.Query
        $headers = @{}

        while ($true) {
            $line = $reader.ReadLine()
            if ($null -eq $line -or $line.Length -eq 0) {
                break
            }
            $colonIndex = $line.IndexOf(":")
            if ($colonIndex -gt 0) {
                $headers[$line.Substring(0, $colonIndex).Trim().ToLowerInvariant()] = $line.Substring($colonIndex + 1).Trim()
            }
        }

        if ($method -eq "OPTIONS") {
            Send-HttpResponse -Stream $stream -StatusCode 204 -Reason "No Content" -Body ""
            return
        }

        if ($method -eq "GET" -and $path -eq "/health") {
            Send-HttpResponse -Stream $stream -StatusCode 200 -Reason "OK" -Body '{"ok":true}'
            return
        }

        if ($method -eq "GET" -and $path -eq "/bindings") {
            $result = Resolve-WarThunderBinding `
                -ActionUuid $query["actionUuid"] `
                -ControlId $query["controlId"] `
                -ControlIds $query["controlIds"] `
                -DefaultHotkeyLabel $query["defaultHotkeyLabel"] `
                -Intent $query["intent"]
            Send-JsonResponse -Stream $stream -StatusCode 200 -Reason "OK" -Value $result
            return
        }

        if ($method -ne "POST" -or $path -ne "/command") {
            Send-HttpResponse -Stream $stream -StatusCode 404 -Reason "Not Found" -Body '{"ok":false,"error":"not found"}'
            return
        }

        $contentLength = 0
        if ($headers.ContainsKey("content-length")) {
            $contentLength = [int] $headers["content-length"]
        }

        $buffer = New-Object char[] $contentLength
        $read = 0
        while ($read -lt $contentLength) {
            $count = $reader.Read($buffer, $read, $contentLength - $read)
            if ($count -le 0) {
                break
            }
            $read += $count
        }

        $body = -join $buffer
        $command = if ($body.Trim()) { $body | ConvertFrom-Json } else { [pscustomobject]@{} }
        $keySpec = Resolve-KeySpec -Command $command
        $phase = Resolve-CommandPhase -Command $command

        if ($command.PSObject.Properties["dryRun"] -and $command.dryRun) {
            $dryRunBody = [pscustomobject]@{
                ok = $true
                dryRun = $true
                phase = $phase
                hotkeyLabel = $keySpec.Label
                vKeyCode = [int] $keySpec.Main.VKeyCode
                scanCode = [int] $keySpec.Main.ScanCode
                keys = @($keySpec.Keys | ForEach-Object {
                    [pscustomobject]@{
                        label = $_.Label
                        vKeyCode = [int] $_.VKeyCode
                        scanCode = [int] $_.ScanCode
                        role = $_.Role
                    }
                })
            } | ConvertTo-Json -Compress -Depth 4
            Send-HttpResponse -Stream $stream -StatusCode 200 -Reason "OK" -Body $dryRunBody
            return
        }

        $sent = Send-KeySpec -KeySpec $keySpec -Phase $phase

        if (-not $sent) {
            $lastError = [WTDeckInput.NativeMethods]::LastError()
            throw "SendInput did not accept the key event. Win32Error=$lastError"
        }

        $logKeys = @($keySpec.Keys)
        if ($phase -eq "up") {
            $logKeys = for ($index = @($keySpec.Keys).Count - 1; $index -ge 0; $index -= 1) {
                @($keySpec.Keys)[$index]
            }
        }
        $keySummary = (@($logKeys) | ForEach-Object {
            if ($_.PSObject.Properties["ScanCode"] -and [int] $_.ScanCode -gt 0) {
                "scan:" + [string] [int] $_.ScanCode
            } else {
                "vk:" + [string] [int] $_.VKeyCode
            }
        }) -join "+"
        Write-CompanionLog -Message "Sent keys $keySummary phase $phase for $($command.intent)"
        Send-HttpResponse -Stream $stream -StatusCode 200 -Reason "OK" -Body '{"ok":true}'
    } catch {
        Write-CompanionLog -Message "Request failed: $($_.Exception.Message) $($_.ScriptStackTrace)"
        if ($Client.Connected) {
            Send-HttpResponse -Stream $Client.GetStream() -StatusCode 500 -Reason "Internal Server Error" -Body '{"ok":false}'
        }
    } finally {
        $Client.Close()
    }
}

function Start-Worker {
    param(
        [int] $WorkerPort,
        [string] $WorkerPidPath
    )

    Initialize-SendInput
    Set-Content -LiteralPath $WorkerPidPath -Value $PID
    Write-CompanionLog -Message "WTDeck companion listening on 127.0.0.1:$WorkerPort"

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse("127.0.0.1"), $WorkerPort)
    $listener.Start()
    try {
        while ($true) {
            $client = $listener.AcceptTcpClient()
            Handle-Client -Client $client
        }
    } finally {
        $listener.Stop()
        Remove-Item -LiteralPath $WorkerPidPath -Force -ErrorAction SilentlyContinue
        Write-CompanionLog -Message "WTDeck companion stopped."
    }
}

Import-DotEnv -Path $envPath

$streamDockAppData = Get-ConfigValue -Name "STREAMDOCK_APPDATA" -DefaultValue (Join-Path $env:APPDATA "HotSpot\StreamDock")
$stateDir = Join-Path $streamDockAppData "wtdeck"
if (-not (Test-Path -LiteralPath $stateDir)) {
    New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
}

if ([string]::IsNullOrWhiteSpace($PidPath)) {
    $PidPath = Join-Path $stateDir "companion.pid"
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $stateDir "companion.log"
}
$script:CompanionLogPath = $LogPath

if ($Stop -or $Restart) {
    Stop-ExistingCompanion -ExistingPidPath $PidPath
    if ($Stop -and -not $Restart) {
        Write-Host "WTDeck companion stopped."
        exit 0
    }
}

if ($Worker) {
    Start-Worker -WorkerPort $Port -WorkerPidPath $PidPath
    exit 0
}

if (Test-CompanionHealth -HealthPort $Port) {
    Write-Host "WTDeck companion already running on http://127.0.0.1:$Port/"
    Write-Host "Companion log: $LogPath"
    exit 0
}

Stop-ExistingCompanion -ExistingPidPath $PidPath

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    (Quote-Argument $PSCommandPath),
    "-Worker",
    "-Port",
    $Port,
    "-PidPath",
    (Quote-Argument $PidPath),
    "-LogPath",
    (Quote-Argument $LogPath)
)

Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -WindowStyle Hidden -WorkingDirectory $repoRoot | Out-Null

$deadline = (Get-Date).AddSeconds(5)
while ((Get-Date) -lt $deadline) {
    if (Test-CompanionHealth -HealthPort $Port) {
        Write-Host "WTDeck companion started on http://127.0.0.1:$Port/"
        Write-Host "Companion log: $LogPath"
        exit 0
    }
    Start-Sleep -Milliseconds 150
}

throw "WTDeck companion did not start on port $Port. Check $LogPath"
