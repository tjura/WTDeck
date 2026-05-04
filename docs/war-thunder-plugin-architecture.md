# War Thunder Stream Dock Plugin Architecture

This plugin is dedicated to War Thunder cockpit controls. The goal is a simulation-style Stream Dock surface where each key behaves like a cockpit switch and reacts to live game state from War Thunder's localhost telemetry API.

## Package Shape

The package source is `plugin/com.wtdeck.warthunder.sdPlugin`.

Key files:

- `manifest.json` declares the official Stream Dock plugin package, actions, icons, code path, and property inspector.
- `config/defaults.json` stores telemetry polling and command adapter defaults.
- `config/actions.json` is the action contract: each action has a Stream Dock UUID, telemetry mapping or readiness mode, state labels, and command intent.
- `plugin/index.html` loads the Stream Dock runtime.
- `plugin/js/war-thunder-client.js` polls War Thunder at `http://127.0.0.1:8111`.
- `plugin/js/state-machines.js` turns normalized telemetry percentages into
  cockpit states such as `UP`, `DOWN`, `OFF`, `ON`, `TRANSIT`, `NO FLIGHT`, or
  `OFFLINE`.
- `plugin/js/key-renderer.js` generates per-key SVG images dynamically, so the button face can react every telemetry tick.
- `plugin/js/action-runtime.js` owns Stream Dock events, context tracking, polling, rendering, and command dispatch.
- `property-inspector/` lets the user choose command adapter, binding label,
  companion URL, and telemetry inversion. It can auto-fill an empty binding
  label from the local companion's read-only War Thunder binding discovery.

## Runtime Flow

1. Stream Dock loads `plugin/index.html` from `manifest.json`.
2. `connectElgatoStreamDeckSocket(...)` opens the SDK WebSocket and registers the plugin.
3. The runtime starts polling War Thunder `/state` and `/indicators`.
4. Raw telemetry is normalized into a snapshot with fields such as
   `gearPercent`, `airbrakePercent`, `flapsPercent`, `gForce`, `iasKmh`,
   `tasKmh`, `throttlePercent`, `altitudeMeters`, `radarAltitudeMeters`, and
   connection validity. The snapshot also exposes `activeFlight` and
   `inactiveReason`; `activeFlight` is stricter than raw War Thunder `valid`
   because it requires a current air vehicle and core flight fields.
5. Each visible Stream Dock key gets a cockpit model based on its action definition.
6. The renderer produces a complete SVG key face and sends it through `setImage`.
7. When a key is pressed, the runtime sends the configured command intent through a command adapter.

The first implementation is intentionally browser-runtime based instead of Node.js. That matches the standard SDK WebSocket flow and avoids shipping `node_modules` just to talk to Stream Dock and localhost.

Local verification on this machine showed War Thunder's `/state` response includes `Access-Control-Allow-Origin: *`, so direct webview polling is a reasonable first runtime choice.

## Actions

The current manifest exposes ten cockpit actions:

- `Landing Gear`: reads `/state` field `gear, %`, falls back to `/indicators`
  gear fields, renders `UP`, `DOWN`, `TRANSIT`, `OFFLINE`, or `NO FLIGHT`,
  and sends the configured landing gear binding through the WTDeck companion.
- `Air Brake`: reads `/state` field `airbrake, %`, falls back to `/indicators`
  airbrake fields, renders `OFF`, `ON`, `TRANSIT`, `OFFLINE`, or `NO FLIGHT`,
  and sends the configured air brake binding through the WTDeck companion.
- `Flaps Up`: reads `/state` field `flaps, %`, falls back to `/indicators`
  flap fields, renders `UP`, `MID`, `DOWN`, `OFFLINE`, or `NO FLIGHT`, and
  sends the configured `ID_FLAPS_UP` binding through the WTDeck companion. The
  button face shows the actual normalized flap percentage on a 0-100 scale
  because available flap detents vary by aircraft.
- `Flaps Down`: uses the same flap telemetry and rendered state as `Flaps Up`,
  but sends the configured `ID_FLAPS_DOWN` binding. War Thunder flap controls
  are detent-step commands, so WTDeck exposes directional actions rather than a
  flap toggle.
- `Auto Landing`: arms or cancels an optional landing assist when enabled. It
  can extend landing gear before landing when the gear is confirmed up and speed
  is at or below `350 km/h`; it then holds wheel brake after touchdown and
  releases brake after stop. Drogue deploy is optional: WTDeck sends the
  configured chute binding only when the latest readiness model is `READY`,
  based on IAS and radar altitude, because War Thunder localhost telemetry does
  not expose structured chute deployed/released state. The button renders
  `READY`, `FAST`, `AIR`, `ARM`, `BRK`, `DRG`, `STOP`, `OFFLINE`, or
  `NO FLIGHT` depending on telemetry and assist phase.
- `Fire Flares`: sends the configured War Thunder `Fire flares` binding through
  the WTDeck companion and renders `READY`, `OFFLINE`, or `NO FLIGHT`. It is a
  separate command from `Fire countermeasures` and `Switch countermeasures`, and
  it ships with no default binding.
- `Fire Chaff`: sends the configured War Thunder `Fire chaff` binding through
  the WTDeck companion and renders `READY`, `OFFLINE`, or `NO FLIGHT`. It is a
  separate command from `Fire countermeasures` and `Switch countermeasures`, and
  it ships with no default binding.
- `G Force`: reads `/state` field `Ny`, falls back to `/indicators` `g_meter`,
  and renders a read-only live G-load indicator.
- `Speed`: reads `/state` fields `IAS, km/h` and `TAS, km/h`, and renders a
  read-only airspeed instrument.
- `Altitude`: reads `/state` field `H, m` and `/indicators` `radio_altitude`,
  then renders a read-only radar altitude plus altitude instrument.

Switch Countermeasures and status tiles are planned future actions, but they
should stay out of the local manifest until each action has its own telemetry
mapping or readiness model, rendered state model, and command behavior ready for
live testing. This keeps testing focused and avoids polluting the user profile
with unfinished controls.

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

All user-facing behavior is gated by `activeFlight`, not only by HTTP
connectivity. When the game is in the hangar, menus, a non-air vehicle, or any
other inactive state, cockpit actions render `NO FLIGHT`, audio alerts stop,
auto landing assist is disarmed, fuel burn estimation resets, and command button
presses are ignored. Key-up releases may still be sent as cleanup so a previously
held companion command is not left pressed. A valid-looking but empty inert
aircraft sample is also inactive; War Thunder can leave stale post-flight data
visible in the hangar with `valid: true`, fuel `0`, no burn, no speed, and no
engine output. To avoid canceling landing automation on a single
`/indicators` timeout, the client can reuse the last confirmed air identity for
up to five missed identity polls, capped at 1.5 seconds, while `/state` remains
valid and the empty-aircraft hangar signature is not present.

No current `/state` or `/indicators` field has been found for drogue chute
deployed/released state, flare or chaff count, selected countermeasure mode, or
countermeasure release confirmation. These command-only buttons must not infer
action success from missing telemetry. Auto Landing uses speed and radar
altitude as optional chute command-readiness gates, but gear and brake
automation remain available without chute readiness. Countermeasure buttons
should only show whether War Thunder active-flight telemetry is valid enough for
the command to be relevant.

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
Gear, `H` for Air Brake, `PageUp` for Flaps Up, `PageDown` for Flaps Down, and
`Shift+G` for Auto Landing's optional chute deploy. Auto Landing assist also
resolves War Thunder's `ID_GEAR` control, default `G`, for one-shot gear
extension before landing and `brake_left_rangeMax` / `brake_right_rangeMax`
controls, default `B`, as a secondary hold-style brake command. Fire Flares and
Fire Chaff have no
bundled defaults; bind War Thunder's separate `Fire
flares` and `Fire chaff` controls and enter those labels in the property
inspector. The companion sends scan-code keyboard
events with `SendInput`; modifier combinations are pressed in order and
released in reverse order. The browser plugin does not call `showOk` or
`showAlert` for normal control presses because those overlays break the
cockpit-style button experience.

The recommended production direction remains a small signed companion executable
that owns keyboard or virtual-device output. The Stream Dock plugin should stay
responsible for UI, settings, telemetry, and command intent.

## Binding Detection

WTDeck can suggest or fill action bindings from the player's active War Thunder
controls without editing game files. The property inspector asks the localhost
companion for `GET /bindings?actionUuid=...`; the companion reads active
`machine.blk` files under `Documents\My Games\WarThunder\Saves`.

The active-file priority is:

1. `Saves\last\production\machine.blk`
2. The newest `Saves\<uid>\production\machine.blk`

The companion parses each action's configured War Thunder control ID, such as
`ID_GEAR`, `ID_AIR_BRAKE`, `ID_FLAPS_UP`, `ID_FLAPS_DOWN`, `ID_CHUTE`,
`brake_left_rangeMax`, `brake_right_rangeMax`, `ID_COUNTERMEASURES_FLARES`, or
`ID_COUNTERMEASURES_CHAFF`, and
converts `keyboardKey:i=N` DirectInput scan codes into WTDeck labels such as
`G`, `H`, `PageUp`, `PageDown`, `B`, or `Shift+G`. If the active file does not contain an explicit
keyboard override for that control, WTDeck falls back to the action's known
default label. If the active file contains only joystick or mouse bindings for
that control, WTDeck reports that no keyboard binding was found instead of
guessing.

This feature is intentionally suggestion/fill only. WTDeck must not modify
War Thunder `.blk` files, unpack packaged game defaults, or require binding
detection for button rendering or manually configured command dispatch.

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
- Avoid assuming default War Thunder keybindings. Expose labels and adapters through the property inspector, and use companion binding detection only as a read-only fill helper.
- Send game input through the companion with explicit key down/key up phases.
- Keep unfinished actions out of the manifest until they have a live-test path.
