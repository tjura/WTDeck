# Testing WTDeck

WTDeck now supports a built-in debug and emulation harness for validating the app without restarting Stream Controller, injecting real keyboard input, or requiring a live War Thunder session.

The harness exposes two validation gates:

- `telemetry` gate: confirms WTDeck parsed the current telemetry into the expected internal state
- `ui` gate: confirms WTDeck published the expected plugin-facing button state
- `panel` gate: confirms WTDeck published the expected information-tile alert state

## Modes

### Live debug mode

Use live telemetry, but keep the run side-effect free:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --debug
```

What this does:

- polls the live War Thunder telemetry endpoints
- prints telemetry and UI state changes to the console as JSON lines
- does not sync or restart Stream Controller
- does not send real keyboard input when a button press is simulated

Use this mode when you want to inspect the current state of the app against a running game session.

### Emulated validation mode

Replay a checked-in scenario file and validate both gates:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --emulate-api .\scenarios\landing-gear-cycle.json
```

What this does:

- replaces the live War Thunder telemetry source with a scripted JSON timeline
- uses deterministic default key bindings for command validation
- validates telemetry expectations per step
- validates plugin-facing UI state per step
- validates plugin-facing alert-tile state per step
- exits with `0` on success and non-zero on failure

This is the preferred workflow for regression checks before opening a pull request.

### 8111 capture mode

Record compact War Thunder localhost API changes for telemetry discovery:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --capture-8111
```

What this does:

- polls `/indicators`, `/state`, `/hudmsg`, `/gamechat`, `/map_obj.json`, `/map_info.json`, and `/mission.json`
- defaults to a 500 ms poll interval
- writes only changed endpoint payloads
- flushes JSONL segment files every 10 seconds
- stores output under `tmp/8111-captures/<timestamp>` unless `--capture-output <dir>` is provided
- does not sync Stream Controller or send keyboard input

While capture is running:

- press `m` when the missile warning/marker is visible
- press `q` to stop cleanly

Useful options:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --capture-8111 --capture-duration 120 --capture-output tmp\8111-captures\missile-test
```

Analyze an existing capture:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --analyze-8111-capture tmp\8111-captures\missile-test
```

The analyzer writes `analysis.md` and `analysis.json` into the capture directory. It highlights fields/messages near marker events and summarizes `/map_obj.json` object changes.

## Console output

The harness writes JSON lines so runs are readable by humans and scripts.

Main event types:

- `telemetry_state`
- `ui_state`
- `panel_state`
- `gate_result`
- `command_result`
- `summary`

Example:

```json
{"event":"telemetry_state","mode":"scenario","step":1,"name":"gear-up","payload":{"available":true,"valid":true,"aircraftType":"a_4n","gearPercent":0,"gear":0,"gearsCommand":0,"gearsLamp":0,"indicatedAirspeedKmh":300}}
{"event":"ui_state","mode":"scenario","payload":{"actionKey":"landing-gear","title":"GEAR UP","statusKey":"up","isBlinking":false,"isEnabled":true,"alertLevel":"None"}}
{"event":"panel_state","mode":"scenario","payload":{"statusKey":"normal","isAvailable":true,"alerts":{"over-g":{"label":"G","value":"1.0","statusKey":"normal","alertLevel":"None","isAvailable":true,"numericValue":1}}}}
{"event":"gate_result","gate":"telemetry","step":1,"name":"gear-up","passed":true}
{"event":"gate_result","gate":"ui","step":1,"name":"gear-up","passed":true,"actionKey":"landing-gear"}
```

## Scenario file format

Scenario files are JSON documents with a fixed polling interval and an ordered list of steps.

Top-level fields:

- `name`: scenario name shown in output
- `stepIntervalMs`: replay interval between steps
- `steps`: ordered timeline

Each step can include:

- `name`
- `indicators`: emulated `/indicators` payload
- `state`: emulated `/state` payload
- `expectTelemetry`: expected parsed state
- `expectUi`: expected plugin-facing button update
- `expectPanel`: expected plugin-facing information-tile update
- `commands`: optional simulated button presses to validate command handling

Supported telemetry expectations:

- `available`
- `valid`
- `aircraftType`
- `gearPercent`
- `gear`
- `gearsCommand`
- `gearsLamp`
- `indicatedAirspeedKmh`
- `loadFactorNy`

Supported UI expectations:

- `actionKey`
- `title`
- `statusKey`
- `isBlinking`
- `isEnabled`
- `alertLevel`

Supported panel expectations:

- `statusKey`
- `isAvailable`
- `alerts`

Each alert expectation can include:

- `label`
- `value`
- `statusKey`
- `alertLevel`
- `isAvailable`
- `numericValue`

Command fields:

- `actionKey`
- `expectedScanCodes`
- `expectedUi`

Checked-in examples live under [`scenarios/`](../scenarios/):

- [landing-gear-cycle.json](../scenarios/landing-gear-cycle.json)
- [landing-gear-damaged.json](../scenarios/landing-gear-damaged.json)
- [landing-gear-retracting.json](../scenarios/landing-gear-retracting.json)
- [flight-alerts-over-g.json](../scenarios/flight-alerts-over-g.json)
- [overspeed-clear-retrigger.json](../scenarios/overspeed-clear-retrigger.json)
- [telemetry-invalid.json](../scenarios/telemetry-invalid.json)
- [telemetry-unavailable.json](../scenarios/telemetry-unavailable.json)

## Recommended workflow

Use both gates before publishing app logic changes:

1. Run the emulated scenario and confirm exit code `0`.
2. Review the `summary` line and ensure both gates passed.
3. Run `pwsh .\build\validate-quality.ps1`.
4. If the change affects live behavior, also run `--debug` against a real War Thunder session and inspect the printed state transitions.

## Current limitations

- The harness covers landing gear, flares, and the first full-size over-G information tile.
- Scenario-mode command validation uses WTDeck default bindings for determinism.
- There is no dedicated CLI for selecting individual validation gates yet; both run together in emulation mode.
