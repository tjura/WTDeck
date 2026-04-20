# Contributing to WTDeck

WTDeck is an experimental project. Contributions are welcome, but the repository
is still shaping its boundaries, workflow, and release model.

## Before you start

- Read [README.md](README.md) for project status and local validation commands.
- Read the architecture and protocol docs in [docs](docs).
- Open an issue first if your change is large, architectural, or introduces a
  new feature area.

Small fixes, focused tests, documentation corrections, and narrow bug fixes are
usually the easiest place to start.

## What we accept

We are open to:

- bug reports
- feature requests that fit the current scope
- focused pull requests
- documentation improvements
- tests that improve confidence in existing behavior

We may decline changes that:

- move business logic into the plugin
- increase coupling between layers
- add broad new scope without prior discussion
- rewrite unrelated parts of the codebase as part of a small fix

## Development expectations

- Keep repository content in English.
- Keep the Stream Controller plugin thin and transport-focused.
- Keep Win32 input isolated in `src/WTDeck.Input.Windows`.
- Update docs when behavior, configuration, or public contracts change.
- Prefer small, reviewable pull requests over large batches of changes.

## Validation

There is no CI yet. Before opening a pull request, run the local validation
commands yourself:

```powershell
dotnet restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet format --verify-no-changes
pwsh .\build\validate-quality.ps1
```

The plugin is currently plain HTML/JavaScript. There is no npm-based build
pipeline yet, so plugin validation is handled through the quality script,
manifest checks, and the relevant .NET tests.

## Pull request guidelines

When you open a pull request:

- explain the problem and the change clearly
- link the related issue when there is one
- keep the PR focused on one problem
- include tests or explain why tests are not practical
- include screenshots, logs, or repro details when the change affects behavior
- avoid unrelated refactors

## Review and merge

WTDeck is currently maintained on a best-effort basis.

- response times may vary
- not every issue or PR will be accepted
- roadmap fit and maintainability matter more than implementation speed

By submitting a contribution, you agree that it may be included in the project
under the repository license.
