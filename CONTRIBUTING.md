# Contributing to WTDeck

WTDeck is an experimental project. Contributions are welcome, but the repository
is still shaping its boundaries, workflow, and release model.

## Before You Start

- Read [README.md](README.md) for current project status.
- Open an issue first if your change is large, changes the project direction, or
  introduces a new area of work.
- Start with focused improvements whenever possible.

Small fixes, documentation corrections, and narrow bug fixes are usually the
easiest place to start.

## What We Accept

We are open to:

- bug reports
- feature requests that fit the current scope
- focused pull requests
- documentation improvements
- improvements that make the project easier to understand, use, or maintain

We may decline changes that:

- add broad new scope without prior discussion
- rewrite unrelated parts of the project as part of a small fix
- make the project harder to review, maintain, or explain

## Contribution Expectations

- Keep repository content in English.
- Keep changes focused and easy to review.
- Update documentation when user-facing behavior or project guidance changes.
- Explain the reason for each meaningful change.
- Avoid unrelated refactors.

## Validation

Before opening a pull request, run the checks that match the changed files:

```powershell
.\scripts\validate-plugin.ps1
.\scripts\deploy-local.ps1 -WhatIf
```

For plugin JavaScript changes, also run `node --check` on the changed `.js`
files. For PowerShell changes, run a parser check before submitting. The GitHub
validation workflow repeats these checks on Windows.

## Pull Request Guidelines

When you open a pull request:

- explain the problem and the change clearly
- link the related issue when there is one
- keep the pull request focused on one problem
- describe how you checked the change
- include screenshots, logs, or examples when they help reviewers understand the
  result

## Review and Merge

WTDeck is currently maintained on a best-effort basis.

- Response times may vary.
- Not every issue or pull request will be accepted.
- Roadmap fit and maintainability matter more than implementation speed.

By submitting a contribution, you agree that it may be included in the project
under the repository license.
