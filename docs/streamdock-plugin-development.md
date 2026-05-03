# StreamDock Plugin Development Research

Last researched: 2026-05-03

This note summarizes what was found while researching how to create and publish an official Stream Dock plugin through the Space marketplace, with extra attention to gaming plugins.

## Official Sources Found

- Space Plugin SDK: <https://sdk.key123.vip/en/>
- SDK overview: <https://sdk.key123.vip/en/guide/overview.html>
- Getting started: <https://sdk.key123.vip/en/guide/get-started.html>
- Terminology: <https://sdk.key123.vip/en/guide/terminology.html>
- Architecture: <https://sdk.key123.vip/en/guide/architecture.html>
- Manifest reference: <https://sdk.key123.vip/en/guide/manifest.html>
- Internationalization: <https://sdk.key123.vip/en/guide/i18n.html>
- Received events: <https://sdk.key123.vip/en/guide/events-received.html>
- Events sent: <https://sdk.key123.vip/en/guide/events-sent.html>
- Registration procedure: <https://sdk.key123.vip/en/guide/registration.html>
- Property Inspector: <https://sdk.key123.vip/en/guide/property-inspector.html>
- Style guide: <https://sdk.key123.vip/en/guide/style-guide.html>
- Stream Dock changelog: <https://sdk.key123.vip/en/guide/changelog.html>
- Help and error reporting: <https://sdk.key123.vip/en/support/help.html>
- Official SDK template repository: <https://github.com/MiraboxSpace/StreamDock-Plugin-SDK>
- Official plugins and examples repository: <https://github.com/MiraboxSpace/StreamDock-Plugins>
- Space StreamDock plugin marketplace: <https://space.key123.vip/StreamDock/plugins>
- Example Space product page provided in the request: <https://space.key123.vip/product?id=1794243538353270793>
- StreamDock support FAQ: <https://support.key123.vip/faqs/streamDock.html>

Adjacent but different official docs also exist under <https://creator.key123.vip/>. Those are direct device-control SDKs for C++, Python, Linux, Windows, and macOS. They are useful for low-level HID/device work, but they are not the main app plugin SDK used by Space marketplace `.sdPlugin` plugins.

## Plugin Model

Stream Dock app plugins are loaded by the Stream Dock desktop application. The plugin communicates with the app over a local WebSocket using JSON. A plugin has one running instance even when the user places the same action on many keys; each placed action is distinguished by its `context`.

A normal plugin package is a `.sdPlugin` directory or archive containing:

- `manifest.json`
- plugin code, such as HTML/JavaScript, Node.js, or an executable
- optional Property Inspector HTML/UI
- icons and other assets
- optional localization JSON files

The official SDK repository includes templates for JavaScript, Vue, Node.js, C++, Qt, and Python. The official docs recommend starting from that template repository rather than building from scratch.

## Local Development Procedure

1. Clone the official SDK template repository:

   ```powershell
   git clone git@github.com:MiraboxSpace/StreamDock-Plugin-SDK.git
   ```

2. Pick the closest template. For a WTDeck-style game plugin, the most practical starting points are JavaScript, Vue, or Node.js. Use Node.js when local filesystem, process, or service calls are needed.

3. Rename the plugin folder to a reverse-DNS `.sdPlugin` identifier, for example:

   ```text
   com.example.wtdeck.sdPlugin
   ```

4. Update every plugin and action UUID in `manifest.json`. The official docs require lowercase alphanumeric characters, periods, and hyphens, in reverse-DNS style.

5. Implement actions and, where needed, a Property Inspector.

6. Install into Stream Dock for testing. The official Windows path is:

   ```text
   C:\Users\{username}\AppData\Roaming\HotSpot\StreamDock\plugins
   ```

   The Vue template can add the plugin automatically through `npm run build` or `npm run dev`; non-Vue templates may need manual copying.

7. Restart Stream Dock after adding a plugin for the first time.

8. Debug through:

   ```text
   http://localhost:23519/
   ```

   Reloading the debug page can refresh a plugin or Property Inspector page. Changes to plugin code generally require restarting Stream Dock.

## Manifest Requirements

The official manifest docs define these top-level required fields:

- `Actions`
- `Author`
- `CodePath`
- `Description`
- `Icon`
- `Name`
- `Version`
- `SDKVersion`
- `OS`

Important optional fields:

- `Category` and `CategoryIcon`
- `CodePathMac` and `CodePathWin`
- `PropertyInspectorPath`
- `URL`
- `ApplicationsToMonitor`
- `Software.MinimumVersion`
- `Nodejs`

Action entries normally include:

- `UUID`
- `Icon`
- `Name`
- `States`
- optional `Settings`
- optional `PropertyInspectorPath`
- optional `SupportedInMultiActions`
- optional `UserTitleEnabled`
- optional `Controllers`
- optional `VisibleInActionsList`
- optional per-action `OS`

Icon size guidance from the style guide:

- action icon: 40 x 40 px
- category icon: 48 x 48 px
- key icon: 128 x 128 px
- plugin icon: 128 x 128 px

The style guide recommends single-color action/category icons with transparent backgrounds, vector images where possible, short plugin names that do not include "Plugin" or "Stream Dock", and `showAlert`/`showOk` for button feedback.

### Node.js Runtime

For Node.js plugins, `manifest.json` can include:

```json
{
  "Nodejs": {
    "Version": "20"
  }
}
```

The official docs say Stream Dock provides built-in Node.js 20. Windows requires Stream Dock `3.10.188.226` or higher for this support. macOS requires `3.10.191.0421` or higher.

## Registration and Communication

JavaScript plugins declare `connectElgatoStreamDeckSocket(...)`. Stream Dock uses Elgato-compatible function names. The function receives the WebSocket port, plugin UUID, register event, and app/device info.

The plugin opens:

```js
const server = new WebSocket('ws://127.0.0.1:' + inPort);
```

When the socket opens, the plugin registers:

```js
server.send(JSON.stringify({
  event: inRegisterEvent,
  uuid: inPluginUUID
}));
```

Compiled executable plugins are launched with command-line arguments:

```text
-port <port> -pluginUUID <uuid> -registerEvent <event> -info <json>
```

Property Inspectors also register through `connectElgatoStreamDeckSocket(...)`, with an extra `inActionInfo` parameter. The Vue templates expose these values through `window.argv`.

## Event Surface

Plugins and Property Inspectors can receive:

- `didReceiveSettings`
- `didReceiveGlobalSettings`

Plugins can also receive:

- `keyDown`
- `keyUp`
- `dialDown`
- `dialUp`
- `dialRotate`
- `willAppear`
- `willDisappear`
- `titleParametersDidChange`
- `deviceDidConnect`
- `deviceDidDisconnect`
- `applicationDidLaunch`
- `applicationDidTerminate`
- `systemDidWakeUp`
- `propertyInspectorDidAppear`
- `propertyInspectorDidDisappear`
- `sendToPlugin`

Property Inspectors can also receive:

- `sendToPropertyInspector`

Plugins and Property Inspectors can send:

- `setSettings`
- `getSettings`
- `setGlobalSettings`
- `getGlobalSettings`
- `openUrl`
- `logMessage`

Plugins can additionally send:

- `setTitle`
- `setImage`
- `showAlert`
- `showOk`
- `setState`
- `sendToPropertyInspector`

Property Inspectors can additionally send:

- `sendToPlugin`

## Property Inspector Pattern

The Property Inspector is an HTML5 UI shown when the user selects an action on the Stream Dock canvas. It runs separately from plugin code and communicates through the same Stream Dock WebSocket bridge.

The usual persistence flow is:

1. Property Inspector reads current action settings from `inActionInfo`.
2. User changes fields in the Property Inspector.
3. Property Inspector sends `setSettings`.
4. Stream Dock persists the settings and sends `didReceiveSettings` to the plugin and Property Inspector.

For Vue templates, the official docs show a reactive `settings` object watched deeply, with changes persisted by sending `setSettings`.

## Internationalization

Localization files live next to `manifest.json`. Officially documented language files:

- `zh_CN.json`
- `de.json`
- `en.json`
- `fr.json`
- `ja.json`
- `ko.json`
- `es.json`

Published plugins may include more files, such as `it.json`, `pt.json`, or `ru.json`. If a localized value is missing, Stream Dock falls back to strings in `manifest.json`.

## Marketplace Publishing Flow

The SDK README says finished plugins can be published to the Space Platform. The public docs do not provide a full written upload checklist, so this section combines official repository guidance with the current Space marketplace web app behavior observed on 2026-05-03.

Recommended official route:

1. Build and test the plugin locally.
2. Package it as a `.sdPlugin` product.
3. Sign in to Space.
4. Go to account/content upload.
5. Choose `Upload Product` and select `Stream Dock Plugin`.
6. Complete the product metadata.
7. Upload the plugin file.
8. Save as draft or upload/submit for review.
9. Wait for marketplace review before it becomes public.

The current Space upload UI includes fields for:

- upload language
- product type
- original type
- product name
- author
- product avatar/plugin icon
- gallery images
- overview
- "what's new" information
- related links
- uploaded product file
- version, validated as `x.y.z`
- supported devices
- Windows/macOS support
- dial support
- free/paid membership and price/frequency fields
- optional prerequisite/front plugin field

For Stream Dock plugin products, the upload code maps product type `1` to:

```text
streamDock/plugin
```

and accepts:

```text
.SDPlugin, .sdPlugin
```

The app also accepts a zipped package if the archive name preserves the plugin suffix before `.zip`, for example `com.example.wtdeck.sdPlugin.zip`.

The client-side upload text references a 20 MB limit. Published marketplace products can be larger, so treat that as current UI behavior rather than a verified platform contract.

## Marketplace Product Types

Space uses `productType` to distinguish marketplace item classes. The request's example URL:

```text
https://space.key123.vip/product?id=1794243538353270793
```

resolves to:

- name: `Flight Simulator`
- author: `VIVRE-MOTION`
- category: `Gaming`
- `productType`: `2`
- file type: `.SDICON`
- version: `1.0.0`

That is a Gaming icon pack, not a Stream Dock plugin. It is still useful as a packaging and marketplace metadata example, but it should not be copied as a plugin architecture reference.

Relevant Space product type mappings observed in the current web app:

- `1`: Stream Dock plugin, `.SDPlugin` or `.sdPlugin`
- `2`: Stream Dock icon pack, `.SDICON`
- `5`: Stream Dock scene/profile, `.SDProfile` or `.streamDockProfile`
- `9`: Stream Dock number keycap, `.SDCap`
- `12`: Stream Dock animated background, `.mp4`

## Gaming Plugin Examples

The official marketplace Gaming plugin category is `productType=1` and `type=3`. These examples were sampled from the Space marketplace and, where noted, downloaded and inspected as `.sdPlugin` zip-format packages.

| Product | Marketplace id | Store metadata | Package observations | Useful lesson |
| --- | --- | --- | --- | --- |
| ETS Hotkeys | `178366994579119` | MiraBox, version `1.0.0`, macOS + Windows, Gaming, about 3.97M downloads at research time | `com.mirabox.hotkey.ets2.sdPlugin`; SDK `1`; 15 actions; no Property Inspector; localization files; hotkey settings embedded per action | Simple game-control packages can be action-heavy and settings-light. This is closest to a pure hotkey game plugin. |
| AAO For MSFS | `20251221004185` | Axis And Ohs, version `1.0.9`, Windows only in Space metadata | `com.lorbysi.aao.sdPlugin`; manifest name `AxisAndOhs Integration`; SDK `2`; `CodePath: code.html`; `PropertyInspectorPath: pi/index.html`; 15 actions; monitors AxisAndOhs Windows executables | Good reference for simulator integration, app monitoring, dynamic values, gauges, and many action types. Marketplace OS metadata should be aligned with manifest OS metadata. |
| Steam Controller | `20260114002477` | MiraBox, version `1.0.0`, Windows only | `com.mirabox.streamdock.steam.sdPlugin`; SDK `1`; `CodePath: plugin/index.js`; `Nodejs.Version: 20`; 8 actions; Property Inspector `index.html` | Best sampled reference for a Node.js game-adjacent plugin in the official store. |
| Flight-Tracker | `178366994578936` | Hy, Pieter and rmroc451, version `0.54.0`, Windows only | `tech.flighttracker.streamdeck.sdPlugin`; SDK `2`; `CodePath: FlightStreamDeck.AddOn.exe`; 26 actions; bundled profiles; action-level Property Inspectors; knob actions | Good compiled-executable reference for advanced simulator telemetry and prebuilt profiles. |
| Trucky | `20250214000388` | Trucky, version `1.0.0`, macOS + Windows | Store overview describes ETS2/ATS telemetry, game state, keystrokes, radio, screenshot, and clip actions | Useful product-scope reference for truck-sim telemetry plus user-triggered game commands. |
| LeagueDeck | `178366994578938` | TimeBlaster, version `2.5.0`, Windows only | Store overview describes League of Legends game-dynamics tracking | Useful example of a game-specific status/tracking plugin. |
| MSFSDock | `20251207001184` | rvoronov, version `0.9.3`, Windows only | Store overview describes MSFS 2024/2020 status lights, simulator events, data display, touchscreen, and knob support | Strong design reference for flight-sim actions that both send events and reflect simulator state. |

## Packaging Observations From Downloaded Store Plugins

Sampled `.sdPlugin` downloads have zip file signatures (`PK 03 04`). After extraction, the plugin root directory contains `manifest.json`.

Observed package shapes:

- `ETS Hotkeys`: mostly manifest, images, and localization. No custom code path was present. It appears to rely on Stream Dock hotkey support through manifest action settings.
- `AAO For MSFS`: HTML code path, `pi` Property Inspector folder, shared JavaScript and settings files.
- `Steam Controller`: Node.js plugin code plus Property Inspector, package metadata, and icons.
- `Flight-Tracker`: Windows executable, action Property Inspectors, localization folders, images, and bundled profiles.

Important discrepancy: the official manifest docs say `CodePath` is required. The published ETS Hotkeys package did not include `CodePath`, apparently because it uses a built-in hotkey style with `KeyboardSupport`. For a new official plugin, prefer following the documented template and include `CodePath` unless StreamDock/MiraBox confirms that a manifest-only hotkey package is acceptable.

## WTDeck Takeaways

- Use a reverse-DNS plugin id from the start, for example `com.<publisher>.wtdeck.sdPlugin`.
- Prefer one clear action per game command or telemetry display. The official style guide explicitly favors splitting complex actions over an overloaded Property Inspector.
- If the first milestone is hotkeys only, a JavaScript or Node.js template still gives more control and clearer SDK compliance than relying on undocumented `KeyboardSupport`.
- If WTDeck needs live state, use `setImage`, `setTitle`, and multi-state actions to reflect aircraft, vehicle, or game status.
- Use `showOk` and `showAlert` for immediate button feedback.
- If monitoring a game executable, add `ApplicationsToMonitor` after verifying the exact executable name on Windows and macOS.
- Keep marketplace metadata and manifest metadata aligned: supported OS, version, author, icon, category, and minimum Stream Dock version should match.
- Build localized strings early. Published official plugins commonly ship many locale files.
- Validate with Stream Dock's local debug page before packaging, then install the exact packaged `.sdPlugin` artifact and test again.

## Open Questions Before Publishing WTDeck

- What publisher reverse-DNS namespace should WTDeck use?
- Will the first version be hotkey-only, live telemetry, or both?
- Which Stream Dock devices must be supported on day one?
- Should support start with Windows only, then add macOS later?
- Which War Thunder executable names and local telemetry interfaces are stable enough for `ApplicationsToMonitor` and live status?
- Does MiraBox require any manual partner/developer approval beyond the Space account upload and review process?
