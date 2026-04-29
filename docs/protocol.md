# IPC Protocol

## Transport

- **HTTP REST API** on `http://127.0.0.1:8730` (loopback only)
- Plugin polls the app for state; app exposes endpoints for commands and heartbeats
- Current protocol version: **4**

## Endpoints

### GET /api/stream-dock/state

Returns the current button and alert-tile state snapshot. Button contexts poll every 500ms; information tiles poll every 100ms.

**Response 200**:
```json
{
  "protocolVersion": 4,
  "appVersion": "0.1.0",
  "timestamp": "2026-04-04T12:00:00Z",
  "state": {
    "gearStatus": "down",
    "gearTitle": "GEAR DOWN",
    "gearBlinking": true,
    "gearAlertLevel": "Info",
    "actions": {
      "landing-gear": {
        "statusKey": "down",
        "title": "GEAR DOWN",
        "isBlinking": true,
        "isEnabled": true,
        "alertLevel": "Info"
      },
      "launch-flares": {
        "statusKey": "ready",
        "title": "FLARES\n42",
        "isBlinking": false,
        "isEnabled": true,
        "alertLevel": "None"
      }
    },
    "alerts": {
      "over-g": {
        "label": "G",
        "value": "10.0",
        "statusKey": "warning",
        "alertLevel": "Warning",
        "isAvailable": true,
        "numericValue": 10.0
      }
    },
    "panel": {
      "statusKey": "warning",
      "isAvailable": true
    }
  }
}
```

**gearStatus values**: `up`, `down`, `extending`, `retracting`, `danger`, `unavailable`, `unknown`

**gearAlertLevel values**: `None`, `Info`, `Warning`, `Danger`

**alert status values**: `normal`, `warning`, `danger`, `unavailable`

The initial information-tile alert is `over-g`. It renders as one full-size tile in a Stream Controller information-display slot. It uses positive `/state` `Ny` only; negative G is displayed as `0.0` and evaluated as normal. Missing or invalid telemetry returns `panel.isAvailable = false` and an unavailable `over-g` alert.

### POST /api/actions/{actionKey}

Triggers an action. The plugin calls this on button press.

**Request**: body is an optional JSON object (currently unused)

**Response 200**:
```json
{ "ok": true, "error": null }
```

**Known action keys**:
- `landing-gear`
- `launch-flares`

The `flight-alerts` StreamDock action is an `Information` controller and does not POST commands.

### PUT /api/stream-controller/status

Heartbeat from the plugin. Sent every 2 seconds while the button is visible.

**Request**:
```json
{ "status": "connected" }
```

**status values**: `connected`, `disconnected`, `connecting`

**Response 204** (no content)

### GET /api/health

Diagnostic endpoint.

**Response 200**:
```json
{
  "status": "ok",
  "protocolVersion": 4,
  "lastClientStatus": "connected",
  "lastHeartbeat": "2026-04-04T12:00:00Z"
}
```

## Plugin Framing

The Stream Controller plugin (`plugin/index.js`) uses the Stream Controller WebSocket SDK for button image rendering and `fetch()` against this HTTP API for state sync and action triggering. No custom framing - it's a regular HTTP client speaking JSON over loopback.

## Versioning

All responses include `protocolVersion`. The plugin tolerates unknown fields on deserialization.
