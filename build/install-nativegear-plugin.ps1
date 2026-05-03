param(
    [string]$Configuration = "Release",
    [string]$UserDataRoot = "",
    [switch]$SkipProfilePatch,
    [switch]$RestartStreamDock
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\WTDeck.NativeGear.Plugin\WTDeck.NativeGear.Plugin.csproj"
$pluginUuid = "com.wtdeck.nativegear"
$publishDir = Join-Path $root "tmp\nativegear-plugin\$pluginUuid.sdPlugin"

if ([string]::IsNullOrWhiteSpace($UserDataRoot)) {
    $UserDataRoot = Join-Path $env:APPDATA "HotSpot\StreamDock"
}

$streamDockExe = "C:\Program Files (x86)\Stream Controller\Stream Controller.exe"
$streamDockWasRunning = $false
if ($RestartStreamDock) {
    $streamDockProcesses = @(Get-Process "Stream Controller" -ErrorAction SilentlyContinue)
    if ($streamDockProcesses.Count -gt 0) {
        $streamDockWasRunning = $true
        $streamDockProcesses | Stop-Process -Force
        Start-Sleep -Milliseconds 750
        Write-Host "Stopped Stream Controller before replacing plugin/profile files"
    }
}

$pluginsDir = Join-Path $UserDataRoot "plugins"
if (-not (Test-Path -LiteralPath $pluginsDir)) {
    throw "StreamDock plugins directory not found: $pluginsDir"
}

dotnet publish $project -c $Configuration -o $publishDir --nologo

$targetDir = Join-Path $pluginsDir "$pluginUuid.sdPlugin"
$resolvedPlugins = [System.IO.Path]::GetFullPath($pluginsDir)
$resolvedTarget = [System.IO.Path]::GetFullPath($targetDir)
$targetLeaf = Split-Path -Leaf $resolvedTarget

if (-not $resolvedTarget.StartsWith($resolvedPlugins, [System.StringComparison]::OrdinalIgnoreCase) -or
    $targetLeaf -ne "$pluginUuid.sdPlugin") {
    throw "Refusing to replace unexpected target path: $resolvedTarget"
}

if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
}

New-Item -ItemType Directory -Path $targetDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $targetDir -Recurse -Force

Write-Host "Installed $pluginUuid at $targetDir"

if (-not $SkipProfilePatch) {
    $profilesDir = Join-Path $UserDataRoot "profiles"
    $oldActionUuid = "com.wtdeck.streamdock.gear-native"
    $newActionUuid = "com.wtdeck.nativegear.gear"

    if (Test-Path -LiteralPath $profilesDir) {
        $patched = 0
        Get-ChildItem -LiteralPath $profilesDir -Recurse -Filter "manifest.json" | ForEach-Object {
            $manifestPath = $_.FullName
            $raw = Get-Content -LiteralPath $manifestPath -Raw
            if ($raw.Contains($oldActionUuid)) {
                $json = $raw | ConvertFrom-Json
                if ($null -ne $json.Actions) {
                    $changed = $false
                    foreach ($actionProperty in $json.Actions.PSObject.Properties) {
                        $action = $actionProperty.Value
                        if ($action.UUID -eq $oldActionUuid) {
                            $action.UUID = $newActionUuid
                            $action.Name = "Landing Gear Native"
                            $changed = $true
                        }
                    }

                    if ($changed) {
                        $backupPath = "$manifestPath.nativegear.bak"
                        if (-not (Test-Path -LiteralPath $backupPath)) {
                            Copy-Item -LiteralPath $manifestPath -Destination $backupPath
                        }

                        $json | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $manifestPath -Encoding UTF8 -NoNewline
                        $patched++
                        Write-Host "Patched native gear action in $manifestPath"
                    }
                }
            }
        }

        Write-Host "Patched $patched profile manifest(s) to use com.wtdeck.nativegear.gear"
    }
}

if ($RestartStreamDock -and $streamDockWasRunning -and (Test-Path -LiteralPath $streamDockExe)) {
    Start-Process -FilePath $streamDockExe -WorkingDirectory (Split-Path -Parent $streamDockExe) -WindowStyle Hidden
    Write-Host "Restarted Stream Controller"
}
