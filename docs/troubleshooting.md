# Troubleshooting

## Common Issues

### "StreamDock not found at ..." warning
The sync service couldn't find the Stream Controller installation. Check:
- Is Stream Controller installed? (default path: `C:\Program Files (x86)\Stream Controller\`)
- Does `%APPDATA%\HotSpot\StreamDock\` exist?
- Override paths via `StreamDock:UserDataRoot` and `StreamDock:InstallDir` in `appsettings.json`

### Button doesn't appear on device
1. Check that WTDeck.App is running (tray icon visible)
2. Open Stream Controller UI and navigate to the "WTDeck" profile
3. Place the Landing Gear button somewhere on your device
4. Restart Stream Controller if the plugin isn't listed (WTDeck.App does this automatically on startup)

### Button stuck on "disabled" / gray
- The plugin can't reach the HTTP API at `http://127.0.0.1:3030`
- Check Windows firewall isn't blocking loopback
- Verify port 3030 isn't in use by another app (`netstat -an | findstr :3030`)
- Check WTDeck.App logs for `HTTP plugin bridge listening on ...`

### No telemetry (status stays "unavailable")
- War Thunder must be running and in a match
- `http://localhost:8111/indicators` only works during active gameplay
- Check you're flying an aircraft with landing gear

### "War Thunder installation not found"
Non-critical. Telemetry still works via `localhost:8111`. Game folder detection is used for future features only.

### No key binding file found
- App uses default G key for landing gear
- To use your custom binding, ensure `.blk` files exist in `%USERPROFILE%\Documents\My Games\WarThunder\Saves\`
- The file must contain `controls{ hotkeys{ }` blocks to be detected

### Sound alerts not working
- Check `appsettings.json` has `"Sound": { "Enabled": true }`
- Ensure system audio output is working
- The tone only plays on `Damaged` state (red blinking)

### Port 3030 conflict
Change it in `appsettings.json`:
```json
"Ipc": { "Port": 3031, "BindAddress": "127.0.0.1" }
```
Note: the plugin's `index.js` has the port hardcoded as `API_BASE`. If you change the app port, also update `API_BASE` in `src/WTDeck.Plugin/plugin/index.js` and re-run the app to re-sync the plugin.

## Diagnostic Endpoint

Visit `http://127.0.0.1:3030/api/health` in a browser to see:
- Protocol version
- Last heartbeat time from plugin
- Last reported plugin status
