# Stream Dock Input Lessons

This note records the live-test result for the first working War Thunder action:
`Landing Gear`.

The important outcome is that telemetry rendering and game input must be treated
as two separate systems. Stream Dock can host the plugin UI and update the button
face in real time, but a custom browser action should not be expected to inherit
the built-in hotkey behavior from Stream Dock's own Toolbox actions.

## Working Solution

The working local setup is:

- Stream Dock loads `plugin/com.wtdeck.warthunder.sdPlugin`.
- The browser runtime polls War Thunder telemetry from `http://127.0.0.1:8111`.
- `Landing Gear` reads `/state` field `gear, %` first.
- It falls back to `/indicators` fields `gears_indicator`, `gears`, and
  `gears_lamp`.
- The key face is rendered dynamically with the immersive cockpit-style visual.
- Pressing the Stream Dock key sends a command to the local WTDeck companion.
- The companion sends Win32 keyboard input to the focused game window.
- War Thunder receives the configured key, currently `G`.
- Telemetry confirms the result by updating the button state.

The command path uses the Star Citizen style:

```text
Stream Dock keyDown -> companion phase "down" -> Win32 key down
Stream Dock keyUp   -> companion phase "up"   -> Win32 key up
```

This worked in the game after replacing the earlier one-shot key tap.

## Why The Companion Exists

The plugin runtime is browser JavaScript inside Stream Dock. It can talk to the
Stream Dock SDK WebSocket, render images, save settings, and call localhost HTTP
endpoints. It cannot reliably synthesize operating-system keyboard input into a
focused game.

Public plugin patterns confirmed the same boundary:

- Flight Tracker StreamDeck uses a compiled plugin and talks to Microsoft Flight
  Simulator through SimConnect events instead of keyboard emulation.
- Star Citizen StreamDeck uses a compiled C# plugin and sends key down/key up
  events through an input simulator.
- SuperMacro-style plugins use their own compiled input layer.
- InputDeck-style projects use a resident backend or virtual-device boundary.

The lesson is that native game input belongs in an executable or backend process.
For WTDeck, the browser plugin owns cockpit UI and command intent. The local
companion owns Windows input.

## Command Adapters

The property inspector intentionally exposes only two options:

- `WTDeck Key Sender`: send the configured game binding through the local
  companion at `http://127.0.0.1:34911/command`.
- `Read Only`: render telemetry but do not send any game input.

Older `Unassigned` and `Read Only` behavior were merged because they were
functionally identical for the user: no command reaches the game. Older
`Native Stream Dock Hotkey` settings are normalized to `WTDeck Key Sender`
because the native hotkey hypothesis failed for custom code actions.

## Correct Implementation Pattern

For each cockpit action:

1. Define the action in `config/actions.json` with a stable command `intent`.
2. Define the primary telemetry field and explicit fallback fields.
3. Normalize raw telemetry in `war-thunder-client.js`.
4. Convert telemetry to cockpit states in `state-machines.js`.
5. Render the button in `key-renderer.js`; do not rely on legacy static state
   images.
6. Use `action-runtime.js` to send companion commands on both `keyDown` and
   `keyUp`.
7. Keep the property inspector limited to settings a pilot can understand:
   adapter, binding label, companion URL, and action-specific telemetry options.
8. Let live telemetry confirm command success instead of showing Stream Dock
   confirmation overlays.

Companion request shape:

```json
{
  "intent": "landing-gear-toggle",
  "hotkeyLabel": "G",
  "phase": "down",
  "source": "streamdock",
  "plugin": "com.wtdeck.warthunder"
}
```

The matching release event sends `"phase": "up"`.

## Antipatterns

Avoid these mistakes:

- Do not assume `VKeyCode`, `NativeCode`, or bundled Toolbox hotkey manifest
  fields will make a custom code action send keyboard input.
- Do not use `showOk` or `showAlert` for normal flight controls. The green
  confirmation overlay breaks cockpit immersion and hides the instrument-like
  key face.
- Do not send only a one-shot tap on Stream Dock `keyDown` for game controls.
  Send key down on `keyDown` and key up on `keyUp`.
- Do not keep separate `Unassigned` and `Read Only` options when both mean no
  command dispatch.
- Do not hardcode War Thunder bindings in the runtime. Store the default in the
  action config and let the property inspector override it.
- Do not leave old static images in the package when the runtime-generated key
  visual is the desired product direction.
- Do not parse endpoint-specific telemetry fields inside the renderer. Normalize
  telemetry before state evaluation.
- Do not treat dry-run HTTP success as proof that Windows input works. Test one
  harmless real key down/up path and inspect the companion log.
- Do not define an incomplete Win32 `INPUT` union. In 64-bit PowerShell the
  managed struct must include the mouse, keyboard, and hardware union members;
  otherwise `SendInput` can reject the event.

## Win32 Input Details

The companion currently uses `SendInput` with scan-code keyboard events:

- `KEYEVENTF_SCANCODE` for key down.
- `KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP` for key up.
- `MapVirtualKey(vkey, 0)` to resolve the scan code from the configured virtual
  key.

The 64-bit `INPUT` struct must marshal to 40 bytes. A 32-byte layout indicates
the union is incomplete and can cause `SendInput` failure.

Keep logging the phase and virtual key:

```text
Sent vkey 71 phase down for landing-gear-toggle
Sent vkey 71 phase up for landing-gear-toggle
```

This makes it clear whether Stream Dock reached the companion and whether the
companion accepted the input event.

## Local Test Checklist

Static checks:

```powershell
.\scripts\validate-plugin.ps1
node --check .\plugin\com.wtdeck.warthunder.sdPlugin\plugin\js\action-runtime.js
.\scripts\deploy-local.ps1 -WhatIf
```

Companion checks:

```powershell
.\scripts\start-companion.ps1 -Restart
Invoke-RestMethod http://127.0.0.1:34911/health
```

Dry-run command checks:

```powershell
$body = @{
  intent = "landing-gear-toggle"
  hotkeyLabel = "G"
  phase = "down"
  dryRun = $true
} | ConvertTo-Json -Compress

Invoke-RestMethod http://127.0.0.1:34911/command -Method Post -ContentType "application/json" -Body $body
```

Repeat with `phase = "up"`.

Live checks:

```powershell
.\scripts\deploy-local.ps1 -NoBackup
.\scripts\test-telemetry.ps1
```

Then focus War Thunder in a test flight and press the Stream Dock key. Expected
result: War Thunder receives `G`, landing gear changes, and the button face
updates from telemetry within the next polling cycle.

## Troubleshooting

If the button visual updates but the game does not react:

- Confirm War Thunder is the focused window.
- Confirm the in-game landing gear binding still uses the configured label.
- Check `http://127.0.0.1:34911/health`.
- Inspect the companion log in Stream Dock AppData under `wtdeck/companion.log`.
- Confirm the log shows both `phase down` and `phase up`.
- If `SendInput` fails, check the logged Win32 error and the `INPUT` struct size.
- If `SendInput` succeeds but the game ignores it, the next fallback is a more
  explicit input backend, such as a signed companion executable or virtual HID
  sender.

If telemetry goes offline:

- Confirm War Thunder is running and a mission or test flight is loaded.
- Open `http://127.0.0.1:8111/state` in a browser.
- Run `.\scripts\test-telemetry.ps1`.
- Do not debug command input until telemetry confirms the aircraft state again.
