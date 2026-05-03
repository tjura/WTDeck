# Project Quality Review

This review captures the current baseline after the first successful in-game
Landing Gear test.

## Current Supported Scope

Version `0.1.0` supports one Stream Dock action:

- `Landing Gear`

It does not currently support flaps, airbrake, countermeasures, flight status
tiles, installer publishing, or store packaging. Those features should be added
only after each one has a telemetry contract, rendered state model, command
adapter behavior, validation coverage, and live in-game test notes.

## Companion Decision

The companion is required for the current architecture.

The custom browser plugin can:

- connect to Stream Dock through the SDK WebSocket
- poll War Thunder localhost telemetry
- save property inspector settings
- render live button images
- send localhost HTTP requests

The custom browser plugin should not be expected to:

- synthesize operating-system keyboard input
- inherit built-in Toolbox hotkey behavior
- send a game-focused keypress only by setting manifest hotkey fields

The failed native-hotkey test and public plugin comparisons point to the same
boundary: custom actions that send game input use an executable, backend,
virtual-device layer, or game-specific API. WTDeck uses a localhost companion for
that boundary.

## Quality Baseline

The repository has the expected open-source support files:

- `LICENSE`
- `README.md`
- `CONTRIBUTING.md`
- `SECURITY.md`
- `SUPPORT.md`
- `CODE_OF_CONDUCT.md`
- GitHub issue templates
- pull request template
- Windows validation workflow

The plugin baseline is intentionally small:

- one manifest action
- one localization action entry
- one telemetry-normalized field, `gearPercent`
- one command intent, `landing-gear-toggle`
- no static legacy state images
- no unfinished action entries in the manifest

## Cleanliness Rules

Keep these rules true before adding new features:

- No stale manifest, config, or localization entries.
- No unused image assets.
- No future-action code unless the action is enabled and tested.
- No `showOk` or `showAlert` for normal cockpit controls.
- No default reliance on Stream Dock native hotkey fields for custom actions.
- No uncommitted generated package output.
- No committed `.env` or machine-specific AppData paths.
- No broad documentation claims beyond the features live-tested in the current
  version.

## Validation Gates

Run these before merging a change:

```powershell
.\scripts\validate-plugin.ps1
.\scripts\deploy-local.ps1 -WhatIf
```

Run JavaScript syntax checks when plugin runtime or property inspector code
changes:

```powershell
$files = Get-ChildItem -LiteralPath "plugin\com.wtdeck.warthunder.sdPlugin" -Recurse -Filter "*.js"
foreach ($file in $files) { node --check $file.FullName }
```

Run PowerShell parser checks when scripts change:

```powershell
$allErrors = @()
foreach ($path in Get-ChildItem -LiteralPath "scripts" -Filter "*.ps1") {
  $tokens = $null
  $errors = $null
  [System.Management.Automation.Language.Parser]::ParseFile($path.FullName, [ref] $tokens, [ref] $errors) | Out-Null
  if ($errors) {
    $allErrors += $errors | ForEach-Object { "$($path.Name): $($_.Message)" }
  }
}
if ($allErrors.Count) { $allErrors | Write-Error; exit 1 }
```

For behavior changes, deploy locally and test in a War Thunder test flight.
