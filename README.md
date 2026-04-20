# WTDeck

WTDeck is a Windows-native companion app plus a thin Stream Deck plugin for War Thunder.

## Product summary

WTDeck should let the user:

- click one Windows executable before starting the game
- read War Thunder telemetry continuously
- map telemetry to dynamic Stream Deck button states
- forward Stream Deck button presses to the game as keyboard input
- install the Stream Deck plugin from inside the app with a single button

The target user experience is:

1. Install WTDeck.
2. Click **Install Plugin** once.
3. Before playing, start `WTDeck.exe`.
4. Start War Thunder.
5. Use Stream Deck normally.

## Recommended architecture

This repository should implement a **single Windows desktop executable** for business logic and a **thin Stream Deck plugin package** for device integration.

### Components

#### 1. WTDeck.App
Windows desktop application, ideally WinUI 3 on .NET 8.

Responsibilities:

- app lifecycle and tray behavior
- settings UI
- one-click plugin installation
- telemetry acquisition
- telemetry parsing
- rule engine
- Stream Deck bridge server
- keyboard input sending through a dedicated adapter
- diagnostics, logs, and health checks

This should be the only process the user launches manually.

#### 2. WTDeck.Plugin
Thin Stream Deck plugin.

Responsibilities:

- receive button events from Stream Deck
- display button title, image, and state
- forward button clicks to `WTDeck.App`
- render current status coming from `WTDeck.App`

This plugin should stay intentionally small. It should not own domain logic.

#### 3. Shared contracts
Shared DTOs and message contracts between the app and plugin.

Responsibilities:

- message schema versioning
- command/event payload types
- button identifiers
- action identifiers
- diagnostics payloads

## Why this architecture

This split gives the best balance of:

- native Windows behavior
- clean ownership boundaries
- testability
- simpler debugging
- easier single-exe distribution for the main app
- simpler Stream Deck plugin maintenance

## Technical stack

### Windows app
- .NET 8
- WinUI 3
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- JSON configuration
- Named Pipes for local IPC
- Win32 `SendInput` via a single input adapter layer

### Stream Deck plugin
- TypeScript
- official Stream Deck SDK
- minimal rendering and event forwarding only

### Tests
- xUnit for .NET unit and integration tests
- Vitest or Jest for plugin-side tests
- ESLint + TypeScript strict mode
- `dotnet format` and Roslyn analyzers for .NET quality checks

## Repository layout

```text
WTDeck/
|- src/
|  |- WTDeck.App/                # WinUI 3 desktop app
|  |- WTDeck.Core/               # domain logic, contracts, rules
|  |- WTDeck.Telemetry/          # telemetry sources and parsers
|  |- WTDeck.Input.Windows/      # SendInput adapter only
|  |- WTDeck.Ipc/                # named pipe transport and message handlers
|  `- WTDeck.Plugin/             # Stream Deck plugin (TypeScript)
|- tests/
|  |- WTDeck.Core.Tests/
|  |- WTDeck.Telemetry.Tests/
|  |- WTDeck.Ipc.Tests/
|  |- WTDeck.App.IntegrationTests/
|  `- WTDeck.Plugin.Tests/
|- docs/
|  |- architecture.md
|  |- protocol.md
|  |- configuration.md
|  `- troubleshooting.md
|- build/
|  |- package-plugin.ps1
|  |- publish-app.ps1
|  `- validate-quality.ps1
|- assets/
|  |- plugin/
|  `- icons/
|- README.md
`- AGENTS.md
```

## Domain boundaries

### Telemetry
Telemetry code must be isolated behind interfaces.

Example direction:

- `ITelemetrySource`
- `ITelemetryParser`
- `FlightState`
- `FlightStateSnapshot`

Do not spread raw telemetry dictionaries across the codebase.

### Rule engine
The rule engine should convert `FlightState` into application-level decisions.

Examples:

- button visual state
- alert severity
- warnings
- disabled/enabled decisions
- action availability

Do not mix rule logic with UI rendering code.

### Stream Deck mapping
A dedicated mapper should convert domain state into button presentation.

Examples:

- title text
- icon key
- state index
- enabled flag
- status badge

### Keyboard input
All keyboard emission must go through a single abstraction.

Examples:

- `IKeyboardSender`
- `WindowsKeyboardSender`
- `KeyChord`
- `VirtualKey`

No direct `SendInput` calls are allowed outside `WTDeck.Input.Windows`.

## Runtime flow

### Startup
1. User starts `WTDeck.exe`.
2. App loads configuration.
3. App validates plugin installation status.
4. App starts IPC server.
5. App starts telemetry reader.
6. App starts rule engine.
7. App publishes button states to plugin clients.

### Button click
1. User presses a Stream Deck key.
2. Plugin sends a command to the app.
3. App resolves the command to a domain action.
4. Input layer emits the configured key chord.
5. App logs the action and updates state if needed.

### Shutdown
1. App stops telemetry reader.
2. App drains and closes IPC.
3. App flushes logs.
4. App exits cleanly.

## Configuration model

Use declarative configuration and avoid hardcoding per-button behavior in code.

Suggested levels:

### Global config
- telemetry source
- IPC endpoint
- logging level
- update cadence
- plugin install path or package asset path

### Profile config
- action-to-key mappings
- thresholds
- aircraft-specific overrides
- display preferences

### Button config
- button id
- action id
- title
- icon set
- visual rule id
- cooldown
- optional tooltip/debug label

## Plugin installation UX

The desktop app should expose an **Install Plugin** button.

Recommended implementation:

1. Bundle the `.streamDeckPlugin` package with the app or generate it during release.
2. On button click, extract or locate the package.
3. Launch the package using the shell.
4. Let Stream Deck handle installation.
5. Verify installation status and surface clear feedback to the user.

Do not implement plugin installation by manually copying unknown files into Stream Deck internal directories unless absolutely required for a special deployment mode.

## Coding standards

### General
- prefer small classes with one responsibility
- keep business logic out of UI and plugin code
- prefer explicit models over loose dictionaries
- prefer immutable DTOs and records where appropriate
- avoid static global state
- log with structure, not with ad hoc string dumps

### .NET
- enable nullable reference types
- treat warnings seriously; prefer warnings as errors in CI
- use dependency injection at boundaries
- keep async flows truly async
- use cancellation tokens for long-running operations
- avoid `Thread.Sleep`
- do not swallow exceptions silently

### TypeScript plugin
- use `strict: true`
- avoid `any`
- keep plugin code dumb and transport-focused
- centralize protocol definitions
- validate incoming messages defensively

## Testing strategy

### Unit tests
Required for:

- telemetry parsing
- rule engine behavior
- configuration validation
- action mapping
- IPC message serialization
- plugin command handling

### Integration tests
Required for:

- app <-> plugin protocol compatibility
- end-to-end command dispatch
- telemetry source to button-state pipeline
- plugin installation flow where practical

### Manual test matrix
Before release, validate at least:

- plugin install on clean machine
- app startup without Stream Deck running
- app startup with Stream Deck running
- telemetry disconnect / reconnect
- repeated key presses
- invalid config handling
- graceful shutdown while connected
- War Thunder active and inactive windows

## Build and test commands

### .NET
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

### .NET formatting and analyzers
```bash
dotnet format --verify-no-changes
dotnet build -c Release -warnaserror
```

### Plugin
```bash
cd src/WTDeck.Plugin
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

## Quality validation gate

A change should be considered merge-ready only if all items below pass:

1. `dotnet build -c Release -warnaserror`
2. `dotnet test -c Release --no-build`
3. `dotnet format --verify-no-changes`
4. plugin lint, typecheck, tests, and build
5. no architecture boundary violations
6. any new behavior includes tests
7. docs updated if contracts or config changed

Suggested one-command validator:

```bash
pwsh ./build/validate-quality.ps1
```

Suggested script responsibilities:

- restore dependencies
- build all .NET projects
- run .NET tests
- run formatting checks
- run plugin lint/typecheck/tests/build
- fail fast on the first broken gate
- return non-zero exit code

## Release process

### App release
Use `dotnet publish` for a Windows self-contained single-file build.

Example:

```bash
dotnet publish ./src/WTDeck.App/WTDeck.App.csproj \
  -c Release \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true
```

### Plugin release
Package the plugin as `.streamDeckPlugin` during CI or release packaging.

### Final distribution
Release artifacts should include:

- `WTDeck.exe`
- plugin package
- default config
- changelog
- license file

## Open source quality checklist

A pull request should not be merged unless it satisfies all of these:

- clear purpose and scope
- code follows architecture boundaries
- tests added or updated
- no dead code or commented-out experiments
- no unchecked hardcoded file paths
- no hidden magic strings for actions
- logs are useful and not noisy
- docs updated where needed
- build passes locally and in CI

## Non-goals

Avoid these unless there is a strong documented reason:

- placing all logic inside the Stream Deck plugin
- direct Win32 input calls from random classes
- hardcoding user keybinds in source
- mixing UI rendering with domain rules
- ad hoc JSON payloads without shared contract types
- plugin reinstall on every normal startup

## First implementation milestones

### Milestone 1: vertical slice
- app boots
- plugin connects
- one telemetry value is parsed
- one button updates visually
- one button press triggers one mapped key chord

### Milestone 2: stability
- reconnect logic
- config validation
- structured logging
- multiple buttons and states
- cooldown and debounce

### Milestone 3: polish
- installer UX
- plugin install verification
- tray mode
- release packaging
- documentation and screenshots

## Contribution expectations

Contributors should preserve the architecture. New features must extend the existing layers rather than bypass them. If a proposed shortcut breaks separation of concerns, prefer to redesign the layer instead of introducing a one-off exception.
