# WTDeck War Thunder Plugin

This folder contains the source package for the Stream Dock plugin:

`com.wtdeck.warthunder.sdPlugin`

The plugin is browser-runtime based. It connects to Stream Dock through the SDK WebSocket and polls War Thunder's local telemetry server at `http://127.0.0.1:8111`.

Version `0.1.0` exposes the `Landing Gear`, `Air Brake`, `Flaps Up`,
`Flaps Down`, `Auto Landing`, `Fire Flares`, `Fire Chaff`, `G Force`, `Speed`,
and `Altitude` actions. New cockpit controls should not be added to the
manifest until their telemetry or readiness mode, rendering, command path, and
live-test notes are complete.

## Layout

- `com.wtdeck.warthunder.sdPlugin/manifest.json` - Stream Dock manifest.
- `com.wtdeck.warthunder.sdPlugin/config/` - action and runtime configuration.
- `com.wtdeck.warthunder.sdPlugin/plugin/` - plugin runtime loaded by Stream Dock.
- `com.wtdeck.warthunder.sdPlugin/property-inspector/` - action settings UI.
- `scripts/validate-plugin.ps1` - static validation for package files.
- `scripts/package-plugin.ps1` - creates a distributable zip in `dist/`.

## Local Install

From the repository root, deploy the plugin and restart Stream Dock:

```powershell
.\scripts\deploy-local.ps1 -NoBackup
```

The local WTDeck key sender companion can be restarted independently:

```powershell
.\scripts\start-companion.ps1 -Restart
```

Use `http://localhost:23519/` for plugin debugging and
`http://127.0.0.1:34911/health` for the companion health check. Binding
detection is available at
`http://127.0.0.1:34911/bindings?actionUuid=com.wtdeck.warthunder.gear.toggle`
and reads War Thunder controls without editing them.

## Validation

Run from the repository root:

```powershell
.\scripts\validate-plugin.ps1
```

## Packaging

Run from the repository root:

```powershell
.\scripts\package-plugin.ps1
```

The output is `dist/com.wtdeck.warthunder.sdPlugin.zip`, matching the suffix-preserving upload style documented by Space.

## Live-Test Notes

Cockpit command actions use the local companion for game input. The plugin sends
key down on Stream Dock `keyDown` and key up on `keyUp`; the companion
translates those phases into Win32 `SendInput` scan-code events. See
`docs/streamdock-input-lessons.md` before adding another command action.
