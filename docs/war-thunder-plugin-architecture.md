# War Thunder Stream Dock Plugin Architecture

This plugin is dedicated to War Thunder cockpit controls. The goal is a simulation-style Stream Dock surface where each key behaves like a cockpit switch and reacts to live game state from War Thunder's localhost telemetry API.

## Package Shape

The package source is `plugin/com.wtdeck.warthunder.sdPlugin`.

Key files:

- `manifest.json` declares the official Stream Dock plugin package, actions, icons, code path, and property inspector.
- `config/defaults.json` stores telemetry polling and command adapter defaults.
- `config/actions.json` is the action contract: each action has a Stream Dock UUID, telemetry mapping, thresholds, state labels, and command intent.
- `plugin/index.html` loads the Stream Dock runtime.
- `plugin/js/war-thunder-client.js` polls War Thunder at `http://127.0.0.1:8111`.
- `plugin/js/state-machines.js` turns normalized telemetry percentages into
  cockpit states such as `UP`, `DOWN`, `OFF`, `ON`, `TRANSIT`, `NO FLIGHT`, or
  `OFFLINE`.
- `plugin/js/key-renderer.js` generates per-key SVG images dynamically, so the button face can react every telemetry tick.
- `plugin/js/action-runtime.js` owns Stream Dock events, context tracking, polling, rendering, and command dispatch.
- `property-inspector/` lets the user choose command adapter, binding label, companion URL, and telemetry inversion.

## Runtime Flow

1. Stream Dock loads `plugin/index.html` from `manifest.json`.
2. `connectElgatoStreamDeckSocket(...)` opens the SDK WebSocket and registers the plugin.
3. The runtime starts polling War Thunder `/state` and `/indicators`.
4. Raw telemetry is normalized into a snapshot with fields such as
   `gearPercent`, `airbrakePercent`, and connection validity.
5. Each visible Stream Dock key gets a cockpit model based on its action definition.
6. The renderer produces a complete SVG key face and sends it through `setImage`.
7. When a key is pressed, the runtime sends the configured command intent through a command adapter.

The first implementation is intentionally browser-runtime based instead of Node.js. That matches the standard SDK WebSocket flow and avoids shipping `node_modules` just to talk to Stream Dock and localhost.

Local verification on this machine showed War Thunder's `/state` response includes `Access-Control-Allow-Origin: *`, so direct webview polling is a reasonable first runtime choice.

## Actions

The current manifest exposes two cockpit actions:

- `Landing Gear`: reads `/state` field `gear, %`, falls back to `/indicators`
  gear fields, renders `UP`, `DOWN`, `TRANSIT`, `OFFLINE`, or `NO FLIGHT`,
  and sends the configured landing gear binding through the WTDeck companion.
- `Air Brake`: reads `/state` field `airbrake, %`, falls back to `/indicators`
  airbrake fields, renders `OFF`, `ON`, `TRANSIT`, `OFFLINE`, or `NO FLIGHT`,
  and sends the configured air brake binding through the WTDeck companion.

Flaps, countermeasures, and status tiles are planned future actions, but they
should stay out of the local manifest until each action has its own telemetry
mapping, rendered state model, and command behavior ready for live testing. This
keeps testing focused and avoids polluting the user profile with unfinished
controls.

## Telemetry Strategy

Poll interval is currently `200 ms`. This is fast enough for responsive cockpit keys while staying conservative compared with War Thunder's browser map, which polls slower JSON endpoints repeatedly and redraws the UI from cached values.

The plugin treats `/state` as primary flight telemetry because it exposes most control percentages directly:

- `gear, %`
- `flaps, %`
- `airbrake, %`
- `IAS, km/h`
- `TAS, km/h`
- `H, m`
- `M`
- `AoA, deg`
- `Ny`
- `Vy, m/s`

`/indicators` is used for cockpit-style fallback fields:

- `gears`
- `gears_indicator`
- `gears_lamp`
- `flaps_indicator`
- `airbrake_indicator`
- `airbrake_lever`
- `mach`
- `g_meter`
- `radio_altitude`
- `type`

The state machines should prefer continuous percentages over binary lamps. Intermediate values are cockpit-relevant and should render as movement, not as stale on/off state.

## Command Dispatch

Stream Dock can render and react to telemetry directly, but sending input into
War Thunder belongs outside the browser plugin. Live testing showed that native
Stream Dock hotkey manifest fields do not make a custom code action behave like
the bundled Toolbox hotkey actions.

The current property inspector exposes two command adapters:

- `WTDeck Key Sender`: posts a command intent to the local companion process at
  `http://127.0.0.1:34911/command`.
- `Read Only`: telemetry rendering only; no command is dispatched.

The working command path mirrors the Star Citizen plugin pattern:

```text
Stream Dock keyDown -> companion phase "down" -> Win32 key down
Stream Dock keyUp   -> companion phase "up"   -> Win32 key up
```

The companion resolves the configured binding label, currently `G` for Landing
Gear and `H` for Air Brake, and sends scan-code keyboard events with
`SendInput`. The browser plugin does not call `showOk` or `showAlert` for
normal control presses because those overlays break the cockpit-style button
experience.

The recommended production direction remains a small signed companion executable
that owns keyboard or virtual-device output. The Stream Dock plugin should stay
responsible for UI, settings, telemetry, and command intent.

## Packaging And Validation

Validate the plugin:

```powershell
.\scripts\validate-plugin.ps1
```

Create an uploadable package:

```powershell
.\scripts\package-plugin.ps1
```

The output is:

`dist/com.wtdeck.warthunder.sdPlugin.zip`

For local development, copy `plugin/com.wtdeck.warthunder.sdPlugin` into:

`C:\Users\{username}\AppData\Roaming\HotSpot\StreamDock\plugins`

Then restart Stream Dock and open the SDK debug page:

`http://localhost:23519/`

## Design Rules For Next Actions

- Keep action definitions in `config/actions.json` first, then mirror any runtime fallback in `plugin/js/generated-config.js`.
- Normalize raw telemetry once in `war-thunder-client.js`; do not parse endpoint-specific fields inside button renderers.
- Treat movement states as first-class cockpit states.
- Do not use map object data for enemy awareness or overlays. This plugin should focus on the player's own aircraft cockpit surface.
- Avoid assuming default War Thunder keybindings. Expose labels and adapters through the property inspector.
- Send game input through the companion with explicit key down/key up phases.
- Keep unfinished actions out of the manifest until they have a live-test path.
