# IPC Protocol

## Transport

- **HTTP REST API** on `http://127.0.0.1:3030` (loopback only)
- Plugin polls the app for state; app exposes endpoints for commands and heartbeats
- Current protocol version: **2**

## Endpoints

### GET /api/stream-dock/state

Returns the current button state snapshot. Called by the plugin every 500ms.

**Response 200**:
```json
{
  "protocolVersion": 2,
  "appVersion": "1.0.0",
  "timestamp": "2026-04-04T12:00:00Z",
  "state": {
    "gearStatus": "down",
    "gearTitle": "GEAR DOWN",
    "gearBlinking": true,
    "gearAlertLevel": "Info"
  }
}
```

**gearStatus values**: `up`, `down`, `extending`, `retracting`, `danger`, `unavailable`, `unknown`

**gearAlertLevel values**: `None`, `Info`, `Warning`, `Danger`

### POST /api/actions/{actionKey}

Triggers an action. The plugin calls this on button press.

**Request**: body is an optional JSON object (currently unused)

**Response 200**:
```json
{ "ok": true, "error": null }
```

**Known action keys**:
- `landing-gear`

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
  "protocolVersion": 2,
  "lastClientStatus": "connected",
  "lastHeartbeat": "2026-04-04T12:00:00Z"
}
```

## Plugin Framing

The StreamDock plugin (`plugin/index.js`) uses the StreamDock WebSocket SDK for button rendering (`setTitle`, `setImage`) and `fetch()` against this HTTP API for state sync and action triggering. No custom framing - it's a regular HTTP client speaking JSON over loopback.

## Versioning

All responses include `protocolVersion`. The plugin tolerates unknown fields on deserialization.
