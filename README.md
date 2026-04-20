# WTDeck

WTDeck is an experimental Windows companion app and Stream Controller plugin for War Thunder.

> Status: experimental, pre-1.0, and not yet a stable release.
> Expect breaking changes, incomplete features, and rough edges while the project is still taking shape.

WTDeck is an unofficial community project. It is not affiliated with, endorsed by, or sponsored by Gaijin Entertainment, War Thunder, HotSpot, or Stream Controller. Product names, logos, and trademarks belong to their respective owners.

## What WTDeck does today

- reads War Thunder telemetry from the local HTTP telemetry endpoints
- maps telemetry into dynamic landing gear button states
- forwards button presses from Stream Controller to the game as keyboard input
- installs and syncs the local plugin/profile assets needed by the app

The current implementation is focused on an initial vertical slice around landing gear state, Stream Controller integration, and Windows input delivery.

## Project status

WTDeck is in active development.

- There is no stable release yet.
- There is no published installer yet.
- The public API and on-disk configuration may still change.
- CI is not configured yet, so contributors must run validation locally.

If you want a polished end-user product, this repository is not there yet. If you want to help shape the project early, this is the right time to get involved.

## Platform and prerequisites

Current target environment:

- Windows 10 or later
- .NET 8 SDK for building from source
- War Thunder with telemetry available on `http://localhost:8111`
- Stream Controller 2.9 or later

## Quick start

There is no packaged public release yet. The current path is source-first:

```powershell
dotnet restore
dotnet build -c Release -warnaserror
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -c Release
```

Typical usage flow:

1. Build and launch `WTDeck.App`.
2. Let the app sync the local Stream Controller plugin/profile assets.
3. Start War Thunder.
4. Use the WTDeck button in Stream Controller.

For architecture and protocol details, see:

- [Architecture](docs/architecture.md)
- [Configuration](docs/configuration.md)
- [Protocol](docs/protocol.md)
- [Testing](docs/testing.md)
- [Troubleshooting](docs/troubleshooting.md)

## Development

### Repository layout

```text
WTDeck/
|- src/
|  |- WTDeck.App/                # Windows host, tray app, DI wiring
|  |- WTDeck.Core/               # domain models, rules, contracts, key bindings
|  |- WTDeck.Telemetry/          # War Thunder telemetry source and mapping
|  |- WTDeck.Input.Windows/      # Win32 keyboard input boundary
|  |- WTDeck.Ipc/                # local HTTP bridge between app and plugin
|  |- WTDeck.StreamDock/         # plugin/profile sync and process control
|  `- WTDeck.Plugin/             # plain HTML/JS Stream Controller plugin assets
|- tests/
|  |- WTDeck.Core.Tests/
|  |- WTDeck.Telemetry.Tests/
|  |- WTDeck.Ipc.Tests/
|  |- WTDeck.App.IntegrationTests/
|  `- WTDeck.StreamDock.Tests/
|- docs/
|- build/
|- assets/
|- README.md
|- CONTRIBUTING.md
|- SECURITY.md
`- CLAUDE.md
```

### Build and test

Core validation commands:

```powershell
dotnet restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet format --verify-no-changes
pwsh .\build\validate-quality.ps1
```

The Stream Controller plugin in this repository is currently plain HTML/JavaScript. There is no `npm`-based build pipeline yet. Plugin validation currently consists of manifest parsing, asset checks, and the relevant .NET integration tests.

### Debug and emulation harness

WTDeck includes a built-in test harness for local validation without a live plugin sync/restart cycle.

Live debug mode:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --debug
```

Deterministic emulation mode:

```powershell
dotnet run --project .\src\WTDeck.App\WTDeck.App.csproj -- --emulate-api .\scenarios\landing-gear-cycle.json
```

The emulation run validates two gates:

- telemetry parsing
- plugin-facing UI output

See [docs/testing.md](docs/testing.md) for the full workflow, console output format, and scenario file schema.

## Architecture direction

WTDeck is intentionally split into:

- a Windows app that owns telemetry, rule evaluation, diagnostics, and input behavior
- a thin Stream Controller plugin that only renders state and forwards events
- shared contracts and deterministic tests around the app/plugin boundary

If you are evaluating a change, keep the plugin thin and keep Win32 input isolated in `WTDeck.Input.Windows`.

## Roadmap

Current priorities:

- stabilize the landing gear vertical slice
- improve app UX and configuration management
- broaden telemetry coverage and aircraft-specific behavior
- package the app/plugin cleanly for public testing
- add CI and release automation once the workflow is stable

## Contributing and support

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.
- Use [GitHub Issues](../../issues) for bugs, feature requests, and support questions once the repo is on GitHub.
- Report security issues privately as described in [SECURITY.md](SECURITY.md).
- Community expectations are defined in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

WTDeck is licensed under the [Apache License 2.0](LICENSE).
