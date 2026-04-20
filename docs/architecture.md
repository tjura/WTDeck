# Architecture

## Component Overview

```
[War Thunder]                          [Stream Controller app]
     |                                           |
  HTTP                                       Local WebSocket SDK
     |                                           |
     v                                           v
[WTDeck.Telemetry] --> [WTDeck.Core Rules] --> [WTDeck.Ipc HTTP :8730] <-- [Stream Controller plugin]
                              |                                                   |
                              v                                                   v
                       [WTDeck.Input.Windows]                           [Stream Controller keys]
                              |
                              v
                       [War Thunder window]

                       [WTDeck.StreamDock] - sync service: plugin install + profile + restart
```

## Projects

| Project | Responsibility |
|---------|---------------|
| WTDeck.Core | Domain models, interfaces, rules engine, key binding parser, status mapper |
| WTDeck.Telemetry | HTTP polling of localhost:8111/indicators |
| WTDeck.Input.Windows | Win32 SendInput for keyboard emission |
| WTDeck.Ipc | HTTP REST API bridge (loopback :8730) |
| WTDeck.StreamDock | Plugin installer, profile builder, process controller |
| WTDeck.App | Console host, system tray, DI wiring, sync orchestration |
| WTDeck.Plugin | Stream Controller SDK v1 plugin (vanilla JS, WebSocket + HTTP poller) |

## Startup Sequence

1. `Program.cs` builds the host and registers all services
2. Tray icon initializes on the main thread
3. `AppHost` starts as a background service:
   a. Starts `HttpPluginBridge` (listener on 127.0.0.1:8730)
   b. Calls `IPluginSyncService.EnsureInstalledAsync`:
      - Stops `Stream Controller.exe` if running
      - Installs/updates plugin at `%APPDATA%\HotSpot\StreamDock\plugins\com.wtdeck.streamdock.sdPlugin\`
      - Creates/updates profile at `%APPDATA%\HotSpot\StreamDock\profiles\{uuid}.sdProfile\manifest.json`
      - Starts `Stream Controller.exe`
   c. Starts telemetry polling loop (100ms cadence)

## Data Flow

### Telemetry -> Button State (read path)
1. `TelemetryPollingService` polls `/indicators` every 100ms
2. `WarThunderTelemetrySource` parses JSON into `FlightState`
3. Polling service fires `StateChanged` only when state changes
4. `AppHost` calls `GearRuleEngine.Evaluate(current, previous)`
5. Rule engine returns `DeckButtonState` with title, icon key, blink, alert
6. `DeckButtonStateMapper` maps icon key -> status key (e.g. `gear-deployed` -> `down`)
7. `AppHost` pushes `ButtonStateUpdate` to `HttpPluginBridge` (in-memory snapshot)
8. Stream Controller plugin polls `GET /api/stream-dock/state` every 500ms and calls `setImage` + `setTitle`

### Button Press -> Keyboard Input (write path)
1. User presses button on Stream Controller
2. Plugin sends `POST /api/actions/landing-gear`
3. `HttpPluginBridge` fires `ButtonPressed` event
4. `AppHost` resolves `ActionId.Gear` via `BlkKeyBindingProvider`
5. `WindowsKeyboardSender` emits the scan code chord via SendInput

## Landing Gear State Machine

| Condition | GearState | Status Key | Blinking | Alert |
|-----------|-----------|------------|----------|-------|
| gears >= 0.95 | Deployed | `down` | Yes | Info |
| gears <= 0.05 | Retracted | `up` | No | None |
| gears increasing | Deploying | `extending` | Yes | Info |
| gears decreasing | Retracting | `retracting` | Yes | Info |
| gears_lamp active + stuck | Damaged | `danger` | Yes | Danger |
| null/invalid state | Disabled | `unavailable` | No | None |

## Sync Service

`WTDeck.StreamDock.Sync.PluginSyncService` keeps the Stream Controller environment in sync with WTDeck:

1. **Plugin install**: Embedded resources in `WTDeck.StreamDock.dll` are extracted to the StreamDock plugins folder. SHA-256 hash comparison per file makes this idempotent.

2. **Profile install**: A "WTDeck" profile with deterministic v5 UUIDs is written to the StreamDock profiles folder. The profile never overwrites user profiles because it uses a dedicated UUID derived from the profile name.

3. **Process restart**: `Stream Controller.exe` is stopped (graceful CloseMainWindow -> Kill fallback) and restarted so it picks up the new files.

All three steps run on every app startup unless `StreamDock:SyncOnStartup = false` in `appsettings.json`.
