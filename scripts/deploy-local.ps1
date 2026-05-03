<#
.SYNOPSIS
Deploys the WTDeck Stream Dock plugin into the local Stream Dock plugins folder.

.DESCRIPTION
Validates the plugin package, optionally backs up the currently deployed copy,
mirrors the working `.sdPlugin` folder into Stream Dock AppData, restarts Stream
Controller, and starts the local WTDeck key sender companion.
#>
param(
    [switch] $NoRestart,
    [switch] $NoBackup,
    [switch] $NoCompanion,
    [switch] $WhatIf,
    [switch] $TailLogs
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

function Get-FullPath {
    param([string] $Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-ChildPath {
    param(
        [string] $ChildPath,
        [string] $ParentPath
    )

    $parentFull = (Get-FullPath $ParentPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $parentFull = $parentFull + [System.IO.Path]::DirectorySeparatorChar
    $childFull = Get-FullPath $ChildPath
    if (-not $childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate on '$childFull' because it is outside '$parentFull'."
    }
}

function Get-LatestLogPath {
    param([string] $StreamDockAppData)

    $logDir = Join-Path $StreamDockAppData "logs"
    if (-not (Test-Path -LiteralPath $logDir)) {
        return $null
    }

    $latest = Get-ChildItem -LiteralPath $logDir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".log", ".txt") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($latest) {
        return $latest.FullName
    }
    return $null
}

function Restart-StreamDock {
    param([string] $ExePath)

    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "Stream Controller executable not found: $ExePath"
    }

    $processName = [System.IO.Path]::GetFileNameWithoutExtension($ExePath)
    $processes = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -eq $processName -or $_.ProcessName -eq "Stream Controller"
    })

    if ($processes.Count -gt 0) {
        Write-Host "Stopping Stream Controller..."
        $processes | Stop-Process -Force
        foreach ($process in $processes) {
            Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Starting Stream Controller..."
    Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -WindowStyle Hidden | Out-Null
}

Import-DotEnv -Path $envPath

$pluginId = Get-ConfigValue -Name "WTDECK_PLUGIN_ID" -DefaultValue "com.wtdeck.warthunder.sdPlugin"
$sourcePath = Get-ConfigValue -Name "WTDECK_PLUGIN_SOURCE" -DefaultValue (Join-Path $repoRoot "plugin\$pluginId")
$streamDockAppData = Get-ConfigValue -Name "STREAMDOCK_APPDATA" -DefaultValue (Join-Path $env:APPDATA "HotSpot\StreamDock")
$pluginsDir = Get-ConfigValue -Name "STREAMDOCK_PLUGINS_DIR" -DefaultValue (Join-Path $streamDockAppData "plugins")
$streamDockExe = Get-ConfigValue -Name "STREAMDOCK_EXE" -DefaultValue "C:\Program Files (x86)\Stream Controller\Stream Controller.exe"
$companionUrl = Get-ConfigValue -Name "WTDECK_COMPANION_URL" -DefaultValue "http://127.0.0.1:34911/command"
$companionPort = ([uri] $companionUrl).Port
$targetPath = Join-Path $pluginsDir $pluginId
$backupRoot = Join-Path $pluginsDir "_wtdeck_backups"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $backupRoot "$pluginId-$timestamp"

Write-Host "Validating plugin..."
& (Join-Path $PSScriptRoot "validate-plugin.ps1")

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Plugin source not found: $sourcePath"
}

Assert-ChildPath -ChildPath $targetPath -ParentPath $pluginsDir
Assert-ChildPath -ChildPath $backupPath -ParentPath $pluginsDir

if ($WhatIf) {
    Write-Host "[WhatIf] Would deploy '$sourcePath' to '$targetPath'."
    if ((Test-Path -LiteralPath $targetPath) -and -not $NoBackup) {
        Write-Host "[WhatIf] Would back up existing plugin to '$backupPath'."
    }
    if (-not $NoRestart) {
        Write-Host "[WhatIf] Would restart Stream Controller from '$streamDockExe'."
    }
    if (-not $NoCompanion) {
        Write-Host "[WhatIf] Would start WTDeck companion sender on port $companionPort."
    }
} else {
    if (-not (Test-Path -LiteralPath $pluginsDir)) {
        New-Item -ItemType Directory -Path $pluginsDir | Out-Null
    }

    if ((Test-Path -LiteralPath $targetPath) -and -not $NoBackup) {
        Write-Host "Backing up existing plugin to $backupPath"
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        Copy-Item -LiteralPath $targetPath -Destination $backupPath -Recurse -Force
    }

    if (Test-Path -LiteralPath $targetPath) {
        Assert-ChildPath -ChildPath (Resolve-Path -LiteralPath $targetPath).Path -ParentPath $pluginsDir
        Remove-Item -LiteralPath $targetPath -Recurse -Force
    }

    Write-Host "Mirroring plugin to $targetPath"
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Get-ChildItem -LiteralPath $sourcePath -Force | Copy-Item -Destination $targetPath -Recurse -Force

    if ($NoRestart) {
        Write-Host "Skipped Stream Controller restart."
    } else {
        Restart-StreamDock -ExePath $streamDockExe
        Start-Sleep -Milliseconds 800
    }

    if ($NoCompanion) {
        Write-Host "Skipped WTDeck companion sender startup."
    } else {
        & (Join-Path $PSScriptRoot "start-companion.ps1") -Restart -Port $companionPort
    }
}

$latestLogPath = Get-LatestLogPath -StreamDockAppData $streamDockAppData
Write-Host "Debug UI: http://localhost:23519/"
if ($latestLogPath) {
    Write-Host "Latest log: $latestLogPath"
} else {
    Write-Host "Latest log: none found under $(Join-Path $streamDockAppData "logs")"
}

if ($TailLogs) {
    if (-not $latestLogPath) {
        throw "Cannot tail logs because no Stream Dock log file was found."
    }
    Write-Host "Tailing log. Press Ctrl+C to stop."
    Get-Content -LiteralPath $latestLogPath -Tail 80 -Wait
}
