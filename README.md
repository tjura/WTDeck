# WTDeck

WTDeck is an experimental Stream Dock plugin for War Thunder cockpit controls.

The current working slice is `Landing Gear`: a Stream Dock key displays live
landing gear state from War Thunder telemetry and sends the configured in-game
binding through a local WTDeck key sender.

> Status: local live-test prototype, pre-release, Windows only.

WTDeck is an unofficial community project. It is not affiliated with, endorsed
by, or sponsored by Gaijin Entertainment, War Thunder, HotSpot, or Stream
Controller. Product names, logos, and trademarks belong to their respective
owners.

## What Works Today

- Polls War Thunder localhost telemetry at `http://127.0.0.1:8111`.
- Renders an immersive dynamic Landing Gear button face.
- Shows `UP`, `DOWN`, `TRANSIT`, `OFFLINE`, or `NO FLIGHT`.
- Sends the landing gear binding, default `G`, through the local companion.
- Deploys the `.sdPlugin` folder into the local Stream Dock plugins directory.
- Restarts Stream Controller for live testing.

## Local Development

Validate the plugin:

```powershell
.\scripts\validate-plugin.ps1
```

Check War Thunder telemetry:

```powershell
.\scripts\test-telemetry.ps1
```

Deploy locally:

```powershell
.\scripts\deploy-local.ps1 -NoBackup
```

Start or restart the key sender companion:

```powershell
.\scripts\start-companion.ps1 -Restart
```

Stream Dock debug UI:

```text
http://localhost:23519/
```

Companion health endpoint:

```text
http://127.0.0.1:34911/health
```

## Repository Layout

- `plugin/com.wtdeck.warthunder.sdPlugin/` - Stream Dock plugin package source.
- `scripts/` - validation, packaging, deployment, telemetry, and companion tools.
- `docs/` - research notes, architecture, telemetry, and live-test lessons.

## Key Documentation

- [War Thunder plugin architecture](docs/war-thunder-plugin-architecture.md)
- [Stream Dock input lessons](docs/streamdock-input-lessons.md)
- [Project quality review](docs/project-quality-review.md)
- [War Thunder localhost telemetry research](docs/war-thunder-localhost-telemetry.md)
- [StreamDock plugin development research](docs/streamdock-plugin-development.md)

## Current Direction

Keep the Stream Dock plugin responsible for telemetry, rendering, settings, and
command intent. Keep Windows input in the local companion process. Do not rely on
native Stream Dock hotkey manifest fields for custom code actions.

Future actions should be added one at a time with the same standard as Landing
Gear: clear telemetry mapping, dynamic button rendering, explicit command
adapter behavior, local deployment automation, and live in-game verification.

## License

This project is licensed under the terms in [LICENSE](LICENSE).
