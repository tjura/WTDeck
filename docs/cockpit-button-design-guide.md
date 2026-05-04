# Cockpit Button Design Guide

This guide defines the visual language for WTDeck Stream Dock actions. It is
based on the current Landing Gear button, but the rules are intentionally general
so future controls can look like part of the same cockpit system.

## Design Intent

WTDeck buttons should feel like compact cockpit instruments, not app tiles,
dashboard cards, or generic Stream Dock shortcuts.

The visual goal is:

- readable at physical Stream Dock size
- calm and instrument-like
- state-first, with command behavior implied by the control shape
- consistent across aircraft systems
- immersive enough to avoid Stream Dock overlay-style feedback

Every button should answer three questions at a glance:

- What system is this?
- What state is it in?
- What control can I press?

## Canvas Model

Design for a square `144 x 144` canvas.

Use a fixed outer shell:

- dark case background
- inset faceplate
- subtle bevel or gradient
- small screws or fasteners
- one stable top title area

Keep the button face stable between states. Only state text, accent color, lamp
brightness, and control position should change. Avoid layout shifts.

## Recommended Layout

Use a three-zone composition:

```text
+----------------------+
|      system title     |
+-----+----------------+
|state|                |
|rail |    control     |
| 1/4 |     bay 3/4    |
|     |                |
+-----+----------------+
```

Approximate ratios:

- Top title: 20 percent of height.
- State rail: 25 percent of active width.
- Control bay: 75 percent of active width.
- Outer margin: 6 to 10 pixels.
- Inner gap between rail and control bay: 6 to 8 pixels.

The Landing Gear face uses this pattern:

- title remains horizontal at the top
- state moves to a vertical rail
- switch gets the larger right-side bay
- old bottom percentage text is removed

## Title

The title names the system, not the current state.

Good examples:

- `LDG GEAR`
- `FLAPS`
- `AIRBRK`
- `DROGUE`
- `FUEL`
- `CANOPY`

Rules:

- Keep title horizontal.
- Keep it short, usually 3 to 8 characters.
- Use uppercase.
- Use compact cockpit abbreviations where they are obvious.
- Do not include instructions such as `PRESS`, `TOGGLE`, or `CLICK`.
- Do not duplicate state text in the title.

## State Rail

The state rail is the primary status indicator.

Use it for:

- `UP`
- `DOWN`
- `OPEN`
- `CLOSED`
- `ARMED`
- `SAFE`
- `OFF`
- `ON`
- `TRANSIT`
- `NO FLIGHT`
- `OFFLINE`

Rules:

- Render state text vertically as stacked characters.
- Keep the rail narrow and stable.
- Include a small lamp near the top of the rail.
- The lamp uses the state tone and opacity.
- Abbreviate long states for readability.

Recommended state abbreviations:

| Full State | Vertical Rail |
| ---------- | ------------- |
| `TRANSIT` | `TRNST` |
| `NO FLIGHT` | `NOFLT` |
| `OFFLINE` | `OFF` |
| `READY` | `RDY` |
| `DEPLOYED` | `DPLYD` |
| `RETRACTED` | `RTRCT` |

Avoid horizontal labels inside the rail. Horizontal text wastes the available
height and becomes too small on the device.

## Control Bay

The control bay represents the user-actionable cockpit control.

It should be larger than the state rail and should visually explain the command:

- Landing gear: vertical switch or lever
- Flaps: indexed lever, segmented detents, or multi-position handle
- Airbrake: guarded push/pull switch or lever
- Drogue chute: guarded pull handle or release lever
- Countermeasures: guarded momentary button
- Canopy: latch or guarded switch

Rules:

- Make the control shape the largest object after the state rail text.
- Keep the control inside a dark recessed bay.
- Use the same accent color as the current state.
- Show position through geometry, not only text.
- Make movement states visibly intermediate.

For continuous percentages, map the value to position. For discrete states, use
fixed positions.

For aircraft systems with variable detents, such as flaps, show the real
normalized position on a compact 0-100 scale. The directional command can be a
small arrow or handle cue; the live scale should remain the source of truth.

## Color System

Use color as state tone, not decoration.

Recommended tones:

| Tone | Use |
| ---- | --- |
| Green | confirmed safe/deployed/available state |
| Amber | movement, transition, caution, intermediate state |
| Dim gray | retracted, inactive, dark, unlit state |
| Cool gray | offline, unknown, no flight |
| Red | armed, warning, dangerous, or destructive action only |

Rules:

- Keep the base UI dark.
- Use one accent color per state.
- Avoid large bright fills.
- Prefer small luminous lamps, outlines, and control handles.
- Do not use decorative gradients unrelated to physical material.

## Typography

Use compact uppercase typography.

Rules:

- Title: small, horizontal, stable.
- State rail: larger, vertical stacked letters.
- Avoid long words.
- Avoid sentence text.
- Avoid bottom labels unless the control genuinely needs a numeric readout.
- Use dynamic sizing only for state labels that can vary in length.

For vertical labels:

- 2 to 4 letters can be larger.
- 5 to 6 letters should be smaller with tighter spacing.
- More than 6 letters should be abbreviated.

## Telemetry Representation

Telemetry should change the instrument, not add explanatory UI.

Good patterns:

- switch handle moves with percent
- lamp brightness changes with state
- accent color changes with state
- rail text updates to the current state

Avoid:

- showing raw percentages by default
- adding explanatory captions
- adding separate status rows for values already shown by geometry
- changing the whole layout when telemetry goes offline

Only add a numeric readout when the number is the main value of that control, for
example temperature, fuel, altitude, or speed.

## Offline And No-Flight States

Offline and no-flight states should look quiet, not broken.

Rules:

- Keep the full control visible.
- Dim the lamp.
- Use cool gray accents.
- State rail should show `OFF` or `NOFLT`.
- Do not show warning overlays.
- Do not use Stream Dock confirmation or alert overlays for normal operation.

## Action Feedback

The preferred feedback loop is:

```text
press button -> send command -> game state changes -> telemetry updates button
```

Do not use `showOk` or `showAlert` for normal cockpit controls. Those overlays
break immersion and cover the instrument face.

Use logs for diagnostics, not visual overlays.

## Future Control Patterns

Use the same shell, title, state rail, and control bay. Change only the control
mechanism.

### Toggle Or Binary Control

Examples: gear, airbrake, canopy, lights.

Use:

- vertical or guarded switch
- top/bottom or left/right positions
- state rail with `ON/OFF`, `UP/DOWN`, or `OPEN/CLSD`

### Multi-Position Control

Examples: flaps, prop pitch, mixture modes.

Use:

- indexed lever
- visible detent marks
- state rail with compact position label
- optional small numeric only if needed

### Momentary Control

Examples: drogue chute, countermeasures, jettison, starter.

Use:

- guarded push button
- red only for destructive or combat-critical actions
- state rail with `RDY`, `ARM`, `SAFE`, or `OFF`
- no persistent pressed state unless telemetry confirms it

### Read-Only Instrument

Examples: speed, altitude, fuel, engine temperature.

Use:

- title at top
- value-focused main bay
- state rail for health or mode
- numeric text is allowed because the number is the primary information

## Design Checklist

Before adding a new button design:

- Is the current supported state readable at device size?
- Does the title identify the system without explaining the command?
- Does the state rail use vertical text?
- Does the control bay communicate the action visually?
- Does color represent state rather than decoration?
- Does telemetry change geometry, lamp, or state text instead of adding clutter?
- Does offline/no-flight remain calm and legible?
- Are long labels abbreviated intentionally?
- Are no legacy static images needed?
- Are normal button presses free of Stream Dock `showOk`/`showAlert` overlays?

## Implementation Guidance

Keep these concepts separate in code:

- state machine: decides `statusKey`, `statusText`, `percent`, and `tone`
- renderer: turns the model into the cockpit face
- action runtime: sends settings, telemetry, and command events
- companion: sends game input

Do not put telemetry parsing or command behavior inside the renderer. The
renderer should receive a clean model and draw it.
