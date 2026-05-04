# WTDeck

WTDeck is an experimental Stream Dock plugin for War Thunder cockpit controls.

The current working slice includes `Landing Gear`, `Air Brake`, `Flaps Up`,
`Flaps Down`, `Drogue Chute`, `Fire Flares`, `Fire Chaff`, `G Force`, `Speed`,
and `Altitude`: Stream Dock keys display live War Thunder telemetry where the
game exposes it and send configured in-game bindings through a local WTDeck key
sender. The property inspector can auto-fill empty binding fields from the
player's active War Thunder controls file without editing War Thunder config.

> Status: local live-test prototype, pre-release, Windows only.

WTDeck is an unofficial community project. It is not affiliated with, endorsed
by, or sponsored by Gaijin Entertainment, War Thunder, HotSpot, or Stream
Controller. Product names, logos, and trademarks belong to their respective
owners.

## What Works Today

- Polls War Thunder localhost telemetry at `http://127.0.0.1:8111`.
- Renders immersive dynamic Landing Gear, Air Brake, Flaps Up, Flaps Down,
  Drogue Chute, Fire Flares, Fire Chaff, G Force, Speed, and Altitude button
  faces.
- Shows cockpit states such as `UP`, `DOWN`, `OFF`, `ON`, `TRANSIT`,
  `READY`, `OFFLINE`, or `NO FLIGHT`.
- Sends the landing gear binding, default `G`, and air brake binding, default
  `H`, through the local companion.
- Sends the directional flaps bindings, defaults `PageUp` and `PageDown`,
  through the local companion. War Thunder flap controls are detent-step
  commands, so WTDeck exposes separate up/down actions instead of a toggle.
- Sends the drogue chute binding, default `Shift+G`, through the local
  companion. War Thunder localhost telemetry does not expose chute deployed
  state, so the button only enables the command in a conservative low-and-slow
  landing envelope rather than fake deployment feedback.
- Supports an optional Drogue auto landing assist. When armed from the Drogue
  key, WTDeck holds the wheel brake binding, default `B`, after touchdown and
  sends the drogue chute command once speed is in range.
- Sends the configured War Thunder `Fire flares` and `Fire chaff` bindings
  through the local companion. These actions intentionally ship without default
  bindings and do not switch countermeasure modes.
- Shows read-only G-load, airspeed, radar altitude, and altitude information as
  Stream Dock Information panels with no game command dispatch.
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

Companion binding detection example:

```text
http://127.0.0.1:34911/bindings?actionUuid=com.wtdeck.warthunder.gear.toggle
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

Future actions should be added one at a time with the same standard as the
existing cockpit controls: clear telemetry mapping, dynamic button rendering,
explicit command adapter behavior, local deployment automation, and live in-game
verification.

## License

This project is licensed under the terms in [LICENSE](LICENSE).
