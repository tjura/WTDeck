# CLAUDE.md

This file gives mandatory instructions to AI coding agents and human contributors working in this repository.

## Mission

Build and maintain a high-quality Windows-native War Thunder + Stream Deck integration with:

- one primary desktop executable for the user
- one thin Stream Deck plugin
- clean architecture boundaries
- strong test coverage
- production-quality open source standards

## Core principles

1. Keep the Stream Deck plugin thin.
2. Keep domain logic in .NET, not in plugin code.
3. Keep Win32 input isolated in one adapter layer.
4. Prefer typed contracts over stringly-typed payloads.
5. Every feature should be testable without the real hardware where possible.
6. Favor maintainability over clever shortcuts.
7. If a shortcut increases coupling, do not take it.

## Architecture rules

### Rule 1: plugin is transport and rendering only
The plugin may:

- receive Stream Deck events
- render titles, images, and states
- forward commands to the app
- receive state updates from the app

The plugin must not:

- own telemetry parsing
- own business rules
- own keybinding logic
- emit keyboard input directly

### Rule 2: app owns behavior
The Windows app owns:

- lifecycle
- telemetry access
- state modeling
- rule evaluation
- command resolution
- keyboard emission
- diagnostics
- plugin installation UX

### Rule 3: one input boundary
Only the input layer may call Win32 input APIs.

Allowed location:

- `src/WTDeck.Input.Windows/`

Forbidden elsewhere:

- direct `SendInput`
- direct P/Invoke for keyboard emission
- ad hoc input helper functions in UI or app services

### Rule 4: protocol is versioned and shared
IPC messages must use shared contracts.

Requirements:

- define message DTOs centrally
- version payloads if protocol changes
- never create ad hoc anonymous payloads in transport code
- update protocol docs when contracts change

### Rule 5: configuration is declarative
Do not hardcode user gameplay mappings in code.

Prefer:

- config files
- validated options objects
- action identifiers mapped to key chords
- visual rule ids mapped to code behavior

## Code generation rules for AI agents

When editing code:

- preserve existing project structure unless a refactor is explicitly justified
- prefer incremental changes over sweeping rewrites
- do not rename public contracts without updating all references and docs
- do not add dependencies casually; explain why they are needed
- do not introduce framework-heavy abstractions without clear value
- do not add hidden background services outside the documented startup path

When adding features:

1. add or update domain model
2. add or update interface boundary
3. add tests
4. update docs if behavior changed
5. keep plugin changes minimal

## Mandatory testing rules

Any non-trivial change must include tests in the most relevant layer.

### Required test locations by concern

- telemetry parsing -> `tests/WTDeck.Telemetry.Tests`
- rule engine -> `tests/WTDeck.Core.Tests`
- IPC protocol -> `tests/WTDeck.Ipc.Tests`
- app integration -> `tests/WTDeck.App.IntegrationTests`
- plugin packaging/profile sync -> `tests/WTDeck.StreamDock.Tests`

### What must be tested

#### Telemetry features
Test:

- valid payload parsing
- partial payload parsing
- invalid field handling
- disconnect/reconnect behavior
- cadence/throttling logic if added

#### Input features
Test:

- action-to-key mapping
- cooldown logic
- chord expansion logic
- invalid mapping validation

Do not rely on real `SendInput` in normal tests. Mock the input interface.

#### Protocol features
Test:

- serialization
- deserialization
- version handling
- unknown field tolerance if intentionally supported
- command routing

#### UI features
Test:

- state mapping from domain to view model
- plugin receives and applies title/state/image updates
- install-plugin command path if logic is testable without real Stream Deck

## Quality gates

Before considering a task complete, validate all applicable checks.

### Standard validation commands

#### .NET
```bash
dotnet restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet format --verify-no-changes
```

#### Plugin
The Stream Controller plugin is currently plain HTML/JavaScript without a
separate npm pipeline. Validate it through:

```bash
pwsh ./build/validate-quality.ps1
```

#### Full validation
```bash
pwsh ./build/validate-quality.ps1
```

If any command fails, the work is not done.

## Style rules

### General
- use descriptive names
- avoid `Helper`, `Utils`, `Manager`, and similar vague class names unless strongly justified
- prefer explicit DTOs and records
- keep methods short and focused
- keep dependencies flowing inward toward domain logic
- delete dead code instead of commenting it out

### .NET
- enable nullable reference types
- use `CancellationToken` for long-running async work
- use structured logging
- do not catch `Exception` unless at a process or boundary layer
- when catching, log context and either recover intentionally or rethrow
- prefer options validation for configuration

### Plugin JavaScript
- keep the plugin transport-focused and intentionally small
- validate external inputs defensively
- keep event handlers small and easy to reason about
- avoid mixing SDK callbacks with domain calculations

## Forbidden shortcuts

AI agents must not do any of the following without explicit human approval:

- move business logic into the plugin because it is "faster"
- duplicate protocol types in multiple places
- bypass tests for "small changes"
- add hardcoded local machine paths
- hardcode personal game keybinds into source
- suppress warnings instead of fixing root causes
- introduce unbounded polling loops
- use sleeps where proper async signaling is possible
- store mutable global singleton state without necessity

## Preferred implementation patterns

### Pattern 1: model first
When implementing a feature, first define or update the model.

Example:

- `FlightState`
- `ActionId`
- `DeckButtonState`
- `ExecuteActionCommand`

### Pattern 2: boundary interface second
Create or update the boundary interface before wiring the implementation.

Examples:

- `ITelemetrySource`
- `IRuleEngine`
- `IKeyboardSender`
- `IPluginBridge`

### Pattern 3: tests before merge
At minimum, write tests in the same task before the change is considered complete.

### Pattern 4: docs with contract changes
If config shape, IPC contracts, or startup flow changes, update:

- `README.md`
- `docs/protocol.md` if protocol changed
- `docs/configuration.md` if config changed

## Decision guide for agents

If you are unsure where code belongs, use this guide:

- "reads telemetry" -> Telemetry layer
- "decides what a button means" -> Core/rule engine
- "renders icon/title/state" -> Plugin
- "sends keyboard input" -> Input.Windows
- "moves bytes/messages between app and plugin" -> IPC layer
- "shows install button or settings" -> App UI

If a change touches more than one of these, keep the boundaries explicit.

## Pull request checklist for agents

Before proposing a final patch, verify:

- architecture still matches this file
- new or changed behavior has tests
- new config is validated
- logs are meaningful
- docs are updated
- no direct input calls escaped the input layer
- plugin stayed thin
- all required commands pass

## Expected CI behavior

CI should fail on:

- build warnings treated as errors
- failed .NET tests
- failed plugin validation checks
- formatting violations

Do not mark work complete if it only builds locally in one subproject.

## Escalation guidance

Raise a design note instead of silently improvising when:

- a change requires breaking the protocol
- a feature seems to force business logic into the plugin
- a hardware dependency blocks deterministic testing
- a new dependency adds significant complexity
- a requested shortcut conflicts with this document

## Final rule

Optimize for a repository that a strong open source maintainer would respect:

- clean boundaries
- typed contracts
- predictable behavior
- good tests
- good docs
- no magic
