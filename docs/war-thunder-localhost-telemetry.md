# War Thunder Localhost Telemetry Research

Last researched: 2026-05-03

This note summarizes War Thunder's local HTTP telemetry surface, especially the real-time flight data available from the browser map server on port `8111`.

## Source Status

There does not appear to be a formal first-party OpenAPI or full official schema document for the localhost API. The most reliable first-party source is the War Thunder browser map page served by the game itself at `http://127.0.0.1:8111/`, plus Gaijin/War Thunder forum posts discussing allowed and disallowed uses.

Useful sources:

- Live browser map entrypoint: `http://127.0.0.1:8111/` while War Thunder is running in a match
- Official forum endpoint discovery discussion: <https://forum.warthunder.com/t/why-dont-we-have-an-api-for-war-thunder/90815?page=3>
- Official forum policy discussion: <https://forum.warthunder.com/t/tools-using-data-provided-on-port-8111/106664/185>
- Follow-up policy clarification thread: <https://forum.warthunder.com/t/cheating-with-localhost-8111/170645>
- Community endpoint documentation: <https://github.com/lucasvmx/WarThunder-localhost-documentation>
- Community API reference mirror: <https://deepwiki.com/lucasvmx/WarThunder-localhost-documentation/2-api-reference>
- Community aircraft telemetry reference: <https://deepwiki.com/lucasvmx/WarThunder-localhost-documentation/2.1-aircraft-telemetry>
- Community map system reference: <https://deepwiki.com/lucasvmx/WarThunder-localhost-documentation/2.2-map-system>
- Python telemetry package docs: <https://pypi.org/project/WarThunder/>
- Python telemetry package source: <https://github.com/PowerBroker2/WarThunder>
- WTRTI real-time information overlay: <https://github.com/MeSoftHorny/WTRTI>
- WT MFD project using localhost telemetry: <https://vavaquin.github.io/Warthunder-MFD/about.html>

## How The Local API Is Exposed

War Thunder starts a local HTTP server on port `8111` while the game is running. The browser map is available at:

```text
http://127.0.0.1:8111/
http://localhost:8111/
```

The API is not only usable by a browser. It returns JSON and image data over plain HTTP, and current responses include permissive CORS headers such as `Access-Control-Allow-Origin: *`.

The official forum describes it as a LAN API, not only a local API. In practice, other devices on the same network may be able to load it through the machine's LAN IP, for example:

```text
http://192.168.x.y:8111/
```

That is useful for second-screen tools, but it is also a privacy and security consideration. If a tool should be local-only, block inbound access to port `8111` with the firewall.

## Endpoint List

The game page itself calls these endpoints:

| Endpoint | Method | Data | Notes |
| --- | --- | --- | --- |
| `/` | GET | HTML | Browser map UI and embedded JavaScript. |
| `/state` | GET | JSON | Current aircraft state, flight dynamics, controls, fuel, and engine data. |
| `/indicators` | GET | JSON | Cockpit/instrument style data: airspeed, altimeter, attitude, controls, engine indicators, gear/flap indicators. |
| `/map_info.json` | GET | JSON | Current map bounds, grid metadata, map generation, and validity. |
| `/map_obj.json` | GET | JSON array | Map objects: aircraft, ground models, bombing points, airfields, objectives, respawns. |
| `/map.img` | GET | JPEG | Current map image used as the browser map background. |
| `/mission.json` | GET | JSON | Mission status and objectives. |
| `/gamechat?lastId=N` | GET | JSON array | Chat messages after the cursor id. Calling `/gamechat` without `lastId` returned HTTP 400 in local testing. |
| `/hudmsg?lastEvt=N&lastDmg=N` | GET | JSON | HUD event and damage message streams after cursor ids. Calling `/hudmsg` without cursor params returned HTTP 400 in local testing. |

The current game page polls the slow data endpoints every 500 ms:

```text
/mission.json
/map_obj.json
/map_info.json
/gamechat?lastId=...
/hudmsg?lastEvt=...&lastDmg=...
/indicators
/state
```

The same page redraws the map every 25 ms, but that redraw uses cached data. It does not mean every API endpoint should be polled at 25 ms.

## Local Verification

On this machine, with War Thunder running, these endpoints responded successfully:

- `/`
- `/state`
- `/indicators`
- `/map_info.json`
- `/map_obj.json`
- `/mission.json`
- `/gamechat?lastId=0`
- `/hudmsg?lastEvt=0&lastDmg=0`
- `/map.img`

`/map.img` returned `image/jpeg`. `/state`, `/indicators`, `/map_info.json`, `/map_obj.json`, and `/mission.json` returned `application/json`.

Sampling `/state` at 200 ms intervals showed changing altitude and fuel values, so the endpoint is suitable for real-time telemetry. StreamDeck/StreamDock button state updates do not need to poll as quickly as an on-screen flight display; 100 to 250 ms is usually enough for dynamic controls, and 250 to 500 ms is enough for most status indicators.

## `/state`

Use this endpoint as the first choice for canonical flight telemetry because many fields include units in their keys.

Request:

```text
GET http://127.0.0.1:8111/state
```

Important field groups:

| Group | Fields |
| --- | --- |
| Validity | `valid` |
| Controls | `aileron, %`, `elevator, %`, `rudder, %`, `flaps, %`, `gear, %`, `airbrake, %` |
| Speed and altitude | `H, m`, `TAS, km/h`, `IAS, km/h`, `M` |
| Aerodynamics | `AoA, deg`, `AoS, deg`, `Ny`, `Vy, m/s`, `Wx, deg/s` |
| Fuel | `Mfuel, kg`, `Mfuel0, kg`, sometimes numbered fuel tank fields such as `Mfuel 1, kg` |
| Engine N | `throttle N, %`, `mixture N, %`, `radiator N, %`, `magneto N`, `power N, hp`, `RPM N`, `manifold pressure N, atm`, `water temp N, C`, `oil temp N, C`, `pitch N, deg`, `thrust N, kgs`, `efficiency N, %` |

The field set is vehicle-dependent. Jets may expose thrust and RPM but `power N, hp` can be `0.0`. Prop aircraft expose prop pitch, manifold pressure, water/oil temperature, and similar piston-engine data. Multiple engines are represented with numbered field names.

For WTDeck, high-value `/state` fields are:

- landing gear: `gear, %`
- flaps: `flaps, %`
- airbrake: `airbrake, %`
- altitude: `H, m`
- IAS/TAS/Mach: `IAS, km/h`, `TAS, km/h`, `M`
- G load: `Ny`
- vertical speed: `Vy, m/s`
- AoA: `AoA, deg`
- fuel remaining: `Mfuel, kg`
- throttle: `throttle 1, %`
- engine health/performance cues: `RPM 1`, `thrust 1, kgs`, `oil temp 1, C`

## `/indicators`

Use this endpoint when you want instrument-style values or when a specific cockpit/control indicator is not available in `/state`.

Request:

```text
GET http://127.0.0.1:8111/indicators
```

Important field groups:

| Group | Fields |
| --- | --- |
| Validity and vehicle | `valid`, `army`, `type` |
| Flight instruments | `speed`, `mach`, `vario`, `altitude_hour`, `altitude_min`, `altitude_10k`, `radio_altitude` |
| Attitude | `aviahorizon_roll`, `aviahorizon_pitch`, `bank`, `turn` |
| Heading | `compass`, `compass1`, `compass2` |
| Controls | `pedals`, `stick_elevator`, `stick_ailerons`, `trimmer` |
| Engine indicators | `rpm_min`, `rpm_hour`, `oil_pressure`, `water_temperature`, `head_temperature`, `fuel`, `fuel_consume`, `throttle` |
| System indicators | `gears`, `gears_indicator`, `gears_lamp`, `flaps`, `flaps_indicator`, `airbrake_lever`, `airbrake_indicator` |
| Airframe/display extras | `g_meter`, `g_meter_min`, `g_meter_max`, `aoa`, `aoa_indexerN`, `wing_sweep_lever`, `blisterN` |

Observed unit notes:

- `speed` appears to be meters per second in current local testing because `speed * 3.6` matched `/state` `IAS, km/h`.
- `mach` mirrors `/state` `M`.
- `vario` closely matches `/state` `Vy, m/s`.
- `altitude_*` fields appear to be cockpit altimeter readings and may not be metric even when `/state` has `H, m`. Prefer `H, m` for canonical altitude.
- Gear and flap indicator fields are often normalized `0.0` to `1.0`, while `/state` may provide percent values.

For WTDeck landing gear work, query both endpoints initially:

1. Prefer `/state` `gear, %` when present.
2. Fall back to `/indicators` `gears_indicator`, `gears`, or `gears_lamp` depending on aircraft behavior.
3. Treat fractional intermediate values as deploying/retracting, not just boolean up/down.

## `/map_info.json` and `/map.img`

`/map_info.json` describes map coordinate metadata. Common fields:

- `valid`
- `grid_size`
- `grid_steps`
- `grid_zero`
- `hud_type`
- `map_generation`
- `map_min`
- `map_max`

`/map.img` returns the JPEG map background. `map_generation` can be used as a cache invalidation hint. If it changes, reload map metadata and the map image.

For a flight telemetry plugin, map image data is usually unnecessary unless building a separate tactical map.

## `/map_obj.json`

`/map_obj.json` returns visible map objects as a JSON array. Common fields:

- `type`
- `color`
- `color[]`
- `blink`
- `icon`
- `icon_bg`
- `x`, `y` for point objects
- `sx`, `sy`, `ex`, `ey` for runway/airfield segment endpoints
- `dx`, `dy` for aircraft direction vectors

Common object types documented by community references and observed locally:

- `aircraft`
- `airfield`
- `ground_model`
- `bombing_point`
- `defending_point`
- `respawn_base_fighter`
- `respawn_base_bomber`

Coordinates are normalized map coordinates, generally between `0` and `1`. Use `map_min` and `map_max` from `/map_info.json` to convert to game-world coordinates. Aircraft heading can be calculated from the direction vector:

```text
headingRadians = atan2(dy, dx)
headingDegrees = atan2(dy, dx) * 180 / pi
```

Policy warning: do not use this endpoint to build enemy-direction overlays, markerless-mode target indicators, ESP-like compass UI, or anything that reveals objects in a way the official map does not. Official forum moderation states that general localhost overlays are usually fine, but using enemy markers in markerless modes as a compass/ESP-style overlay is not approved.

For WTDeck's first milestones, avoid `/map_obj.json` unless there is a clear non-combat, user-facing need that mirrors the official browser map.

## `/mission.json`

`/mission.json` returns mission state:

- `status`, such as `running` or `fail`
- `objectives`, usually an array or `null`
- objective fields such as `primary`, `status`, and `text`

This endpoint is useful for generic "in mission" checks, objective summaries, or low-frequency status indicators. It is not needed for landing gear, flight controls, or engine telemetry.

## `/gamechat`

Request with a cursor:

```text
GET http://127.0.0.1:8111/gamechat?lastId=0
```

Returns a JSON array of messages:

- `id`
- `msg`
- `sender`
- `enemy`
- `mode`

The community docs note that ids are cursor-like and may continue across matches during a game session. Store the latest id seen and request only newer messages.

This is not needed for flight telemetry.

## Landing Gear Damage Investigation

Live investigation on 2026-05-03 tested whether War Thunder exposes structured
landing gear damage or aircraft condition through `localhost:8111`.

Test aircraft/session:

- `/indicators` `type`: `a_4n`
- `/indicators` `army`: `air`
- valid flight telemetry throughout the capture
- landing gear extended during sampling

Endpoints confirmed reachable:

| Endpoint | Result | Notes |
| --- | --- | --- |
| `/` | 200 | Browser map UI and JavaScript. |
| `/state` | 200 | Aircraft state/control telemetry. |
| `/indicators` | 200 | Cockpit/instrument telemetry. |
| `/map_info.json` | 200 | Map metadata. |
| `/map_obj.json` | 200 | Map objects. |
| `/mission.json` | 200 | Mission status. |
| `/hudmsg?lastEvt=0&lastDmg=0` | 200 | HUD event and damage message streams. |
| `/gamechat?lastId=0` | 200 | Chat stream. |
| `/map.img` | 200 | Map image. |

Additional damage/aircraft-condition endpoint guesses returned no usable
structured data:

```text
/damage
/damage.json
/damage_model
/damage_model.json
/dm
/dm.json
/aircraft
/aircraft.json
/vehicle
/vehicle.json
/cockpit
/cockpit.json
/system
/systems
/events
/eventlog
```

The game browser page itself referenced only the known endpoints:

```text
/mission.json
/map_obj.json
/map_info.json
/gamechat?lastId=...
/hudmsg?lastEvt=...&lastDmg=...
/indicators
/state
/map.img
```

Observed current `/state` keys for `a_4n`:

```text
valid
aileron, %
elevator, %
rudder, %
flaps, %
gear, %
airbrake, %
H, m
TAS, km/h
IAS, km/h
M
AoA, deg
AoS, deg
Ny
Vy, m/s
Wx, deg/s
Mfuel, kg
Mfuel0, kg
Mfuel 1, kg
Mfuel0 1, kg
Mfuel 2, kg
Mfuel0 2, kg
throttle 1, %
power 1, hp
RPM 1
manifold pressure 1, atm
oil temp 1, C
thrust 1, kgs
efficiency 1, %
```

Observed current `/indicators` keys for `a_4n`:

```text
valid
army
type
speed
pedals1
pedals2
pedals3
pedals4
stick_elevator
stick_ailerons
vario
altitude_hour
altitude_min
altitude_10k
altitude1_min
altitude1_10k
radio_altitude
aviahorizon_roll
aviahorizon_roll1
aviahorizon_pitch1
bank
turn
compass
compass1
compass2
clock_hour
clock_min
clock_sec
rpm_min
rpm_hour
oil_pressure
water_temperature
fuel
fuel_consume
airbrake_lever
gears
flaps
trimmer
throttle
weapon2
weapon4
flaps_indicator
gears_indicator
trimmer_indicator
mach
g_meter
g_meter_min
g_meter_max
aoa
aoa_indexer1
aoa_indexer2
aoa_indexer3
blister1
blister2
blister3
blister4
blister5
blister6
blister7
blister8
blister12
```

Gear-specific capture results from two live polling passes:

| Field | Result |
| --- | --- |
| `/state` `gear, %` | Stayed exactly `100`. |
| `/indicators` `gears` | Stayed exactly `1.0`. |
| `/indicators` `gears_indicator` | Stayed near `1.0`; observed minor jitter around roughly `0.996` to `1.0`. |
| `/indicators` `gears_lamp` | Not present for `a_4n`. |
| `/hudmsg` new damage messages | No new damage messages during the focused capture. |
| `/hudmsg` new events | No new events during the focused capture. |

Conclusion:

- The accessible API exposes landing gear position/indicator state.
- It does not expose structured landing gear health, wheel status, strut status,
  hydraulic status, left/right/nose gear damage, or "gear destroyed" data.
- `/hudmsg.damage` is the only accessible channel that might report gear damage,
  but it is a text stream, not structured telemetry, and no gear-specific message
  was observed in this test.
- `gears_indicator` can jitter slightly below `1.0` even when `/state` `gear, %`
  and `/indicators` `gears` remain fully down. Treat near-1.0 values as down,
  not as damage or transit.

WTDeck implementation rule:

1. Continue using `/state` `gear, %` as primary landing gear state.
2. Continue falling back to `/indicators` `gears_indicator`, `gears`, then
   `gears_lamp` when present.
3. Clamp normalized gear values near fully down/up to avoid false transit flicker.
4. Do not add a persistent `DAMAGED` gear state from structured telemetry,
   because the API does not currently provide one.
5. A future best-effort warning could watch `/hudmsg.damage` for text containing
   `gear`, `wheel`, or localized equivalents, but that should be documented as
   opportunistic and not authoritative.

## `/hudmsg`

Request with cursors:

```text
GET http://127.0.0.1:8111/hudmsg?lastEvt=0&lastDmg=0
```

Returns:

- `events`: array
- `damage`: array

Message objects can include:

- `id`
- `msg`
- `sender`
- `enemy`
- `mode`
- `time`

This can expose crash, damage, kill, and system messages. It may be useful for future button feedback or event-driven status, but it is not required for core flight telemetry.

## Data Validity And Failure Modes

Expect these states:

- War Thunder not running: connection refused or timeout.
- Game in menus: root page may load, but telemetry may be invalid, stale, empty, or missing.
- Vehicle type unsupported: `valid` may be false or fields may be missing.
- Non-air modes: aircraft telemetry fields may be absent or not meaningful.
- Aircraft-specific panels: keys vary by airframe, engine count, and systems.
- Game version changes: fields can be added, renamed, or removed without formal API versioning.

Implementation rules:

- Always check `valid`.
- Treat every field as optional.
- Parse by exact key name, including commas, spaces, and units.
- Keep raw JSON snapshots during development so missing-field cases can be diagnosed.
- Use short HTTP timeouts.
- Do not block the StreamDock plugin UI or WebSocket callback thread on API polling.

## Recommended Polling For WTDeck

For a StreamDock plugin or companion app:

| Data | Endpoint | Suggested polling |
| --- | --- | --- |
| Gear/flaps/airbrake button state | `/state`, fallback `/indicators` | 100 to 250 ms |
| Speed, altitude, fuel, G-load display | `/state` | 250 ms |
| Instrument-specific display values | `/indicators` | 250 ms |
| Mission status | `/mission.json` | 1000 to 2000 ms |
| Map objects | `/map_obj.json` | 500 to 1000 ms, only if needed |
| Chat/HUD messages | `/gamechat`, `/hudmsg` | cursor-based, 1000 to 5000 ms |
| Map image | `/map.img` | only when `map_generation` changes |

Avoid 25 ms API polling. The game page uses 25 ms for redrawing, not for refetching telemetry.

## Compliance Boundaries

The safe project direction is to use only own-aircraft telemetry and normal action feedback:

- gear state
- flap state
- airbrake state
- speed/altitude/Mach/G/fuel
- engine status
- mission running/not running

Avoid:

- enemy marker overlays
- markerless-mode target direction or range displays
- ESP-like map-to-compass or map-to-HUD conversion
- scraping the game image or memory
- modifying game files, game process memory, or rendered game images
- automating actions beyond normal user-configured button presses

The official forum position is nuanced: general localhost data overlays are described as not normally bannable, but using exposed data to show enemy markers in markerless modes or as a compass/ESP-style overlay is not approved.

## WTDeck Design Takeaways

- Build the data layer around `/state` first.
- Keep `/indicators` as an auxiliary source for cockpit/indicator values and gear/flap fallbacks.
- Treat gear as a state machine:
  - `0`: retracted/up
  - `100` or `1.0`: deployed/down, depending on endpoint
  - intermediate: moving
  - missing/invalid: unknown
- Debounce state transitions so button icons do not flicker around fractional values.
- Use the local API from a companion process if the StreamDock plugin sandbox makes direct polling awkward.
- Keep map object support out of the initial plugin unless it is limited to official-map-equivalent views and explicitly reviewed for fair-play risk.
- Add a diagnostics page or log mode that captures endpoint availability and field names without exposing player names or chat content.

## Quick Manual Checks

When War Thunder is running in a flight mission:

```powershell
Invoke-RestMethod http://127.0.0.1:8111/state
Invoke-RestMethod http://127.0.0.1:8111/indicators
Invoke-RestMethod http://127.0.0.1:8111/map_info.json
Invoke-RestMethod 'http://127.0.0.1:8111/hudmsg?lastEvt=0&lastDmg=0'
```

To inspect the game's own endpoint usage:

1. Open `http://127.0.0.1:8111/`.
2. Open browser developer tools.
3. Check the Network tab or the page source.
4. Look for `updateSlow()` and the AJAX calls listed above.
