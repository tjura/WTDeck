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

    $vkey = $null
    if ($Command -and $Command.PSObject.Properties["vKeyCode"]) {
        $vkey = [int] $Command.vKeyCode
    } elseif ($Command -and $Command.PSObject.Properties["hotkeyLabel"]) {
        $label = ([string] $Command.hotkeyLabel).Trim()
        if ($label.Length -eq 1) {
            $vkey = [int][char] $label.ToUpperInvariant()
        } else {
            switch -Regex ($label.ToUpperInvariant()) {
                "^(SPACE|SPACEBAR)$" { $vkey = 32; break }
                "^(ESC|ESCAPE)$" { $vkey = 27; break }
                "^ENTER$" { $vkey = 13; break }
                "^TAB$" { $vkey = 9; break }
                "^UP$" { $vkey = 38; break }
                "^DOWN$" { $vkey = 40; break }
                "^LEFT$" { $vkey = 37; break }
                "^RIGHT$" { $vkey = 39; break }
            }
        }
    }

    if (-not $vkey -and $Command -and $Command.intent -eq "landing-gear-toggle") {
        $vkey = 71
    }

    if (-not $vkey -or $vkey -lt 1 -or $vkey -gt 255) {
        throw "Unsupported hotkey label."
    }

    return [uint16] $vkey
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
        $path = $parts[1]
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
        $vkey = Resolve-KeySpec -Command $command
        $phase = Resolve-CommandPhase -Command $command

        if ($command.PSObject.Properties["dryRun"] -and $command.dryRun) {
            $dryRunBody = '{{"ok":true,"dryRun":true,"phase":"{0}","vKeyCode":{1}}}' -f $phase, $vkey
            Send-HttpResponse -Stream $stream -StatusCode 200 -Reason "OK" -Body $dryRunBody
            return
        }

        switch ($phase) {
            "down" { $sent = [WTDeckInput.NativeMethods]::SendVirtualKeyDown($vkey); break }
            "up" { $sent = [WTDeckInput.NativeMethods]::SendVirtualKeyUp($vkey); break }
            default { $sent = [WTDeckInput.NativeMethods]::SendVirtualKeyTap($vkey); break }
        }

        if (-not $sent) {
            $lastError = [WTDeckInput.NativeMethods]::LastError()
            throw "SendInput did not accept the key event. Win32Error=$lastError"
        }

        Write-CompanionLog -Message "Sent vkey $vkey phase $phase for $($command.intent)"
        Send-HttpResponse -Stream $stream -StatusCode 200 -Reason "OK" -Body '{"ok":true}'
    } catch {
        Write-CompanionLog -Message "Request failed: $($_.Exception.Message)"
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
