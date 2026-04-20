# Configuration

## appsettings.json

```json
{
  "Telemetry": {
    "BaseUrl": "http://localhost:8111",
    "PollIntervalMs": 100,
    "HttpTimeoutMs": 2000
  },
  "Ipc": {
    "Port": 8730,
    "BindAddress": "127.0.0.1"
  },
  "StreamDock": {
    "SyncOnStartup": true,
    "DeviceUUID": "CN001V3Device",
    "DeviceSerialNumber": "8730DB78224F",
    "DeviceModel": "20GBA9901",
    "ProfileName": "WTDeck",
    "PluginUuid": "com.wtdeck.streamdock"
  }
}
```

## Sections

### Telemetry
- `BaseUrl`: War Thunder telemetry endpoint (default: `http://localhost:8111` - do not change)
- `PollIntervalMs`: polling frequency for `/indicators`
- `HttpTimeoutMs`: timeout per HTTP call

### Ipc
- `Port`: loopback HTTP port the plugin connects to (default: 8730)
- `BindAddress`: must stay on `127.0.0.1` to avoid firewall/UAC issues

### StreamDock
- `SyncOnStartup`: if `true`, install plugin + profile and restart Stream Controller on app start
- `DeviceUUID` / `DeviceSerialNumber` / `DeviceModel`: target device identifiers (must match your physical device)
- `ProfileName`: the dedicated profile name. Does not overwrite your existing profiles.
- `PluginUuid`: the plugin package UUID (installed at `%APPDATA%\HotSpot\StreamDock\plugins\{uuid}.sdPlugin\`)

## Auto-Detection

### Game Folder
Checks Steam registry (`HKLM\SOFTWARE\WOW6432Node\Valve\Steam`) and common install paths. Not critical - telemetry still works via localhost:8111.

### Key Bindings
Searches `%USERPROFILE%\Documents\My Games\WarThunder\Saves\`, picks the most recently modified `.blk` file containing `controls{ hotkeys{ }` blocks. Falls back to scan code 34 (G key) if `ID_GEAR` is not found.

### StreamDock Install Directory
Checks `C:\Program Files (x86)\Stream Controller\` and `C:\Program Files\Stream Controller\` for `Stream Controller.exe`. Can be overridden via `StreamDock:InstallDir` in config.
