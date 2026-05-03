(function () {
  window.WTDECK_DEFAULTS = {
    schemaVersion: 1,
    telemetry: {
      baseUrl: "http://127.0.0.1:8111",
      pollIntervalMs: 200,
      requestTimeoutMs: 120
    },
    commands: {
      defaultAdapter: "companion-http",
      companionTimeoutMs: 250,
      companionUrl: "http://127.0.0.1:34911/command"
    }
  };

  window.WTDECK_ACTION_CONFIG = {
    schemaVersion: 1,
    actions: {
      "com.wtdeck.warthunder.gear.toggle": {
        id: "gear",
        shortLabel: "GEAR",
        panelLabel: "LDG GEAR",
        kind: "toggle",
        telemetry: {
          normalizedField: "gearPercent",
          primaryEndpoint: "state",
          primaryRawField: "gear, %",
          fallbackEndpoint: "indicators",
          fallbackRawFields: ["gears_indicator", "gears", "gears_lamp"]
        },
        thresholds: { offMax: 5, onMin: 95 },
        states: { off: "UP", on: "DOWN", moving: "TRANSIT", unknown: "NO FLIGHT" },
        command: {
          intent: "landing-gear-toggle",
          defaultHotkeyLabel: "G",
          adapter: "companion-http",
          notes:
            "Native Stream Dock hotkey settings do not dispatch for custom code actions; use the local WTDeck companion sender."
        }
      },
      "com.wtdeck.warthunder.airbrake.toggle": {
        id: "airbrake",
        shortLabel: "AIRBRK",
        panelLabel: "AIRBRK",
        kind: "toggle",
        telemetry: {
          normalizedField: "airbrakePercent",
          primaryEndpoint: "state",
          primaryRawField: "airbrake, %",
          fallbackEndpoint: "indicators",
          fallbackRawFields: ["airbrake_indicator", "airbrake_lever"]
        },
        thresholds: { offMax: 5, onMin: 95 },
        states: { off: "OFF", on: "ON", moving: "TRANSIT", unknown: "NO FLIGHT" },
        command: {
          intent: "airbrake-toggle",
          defaultHotkeyLabel: "H",
          adapter: "companion-http",
          notes:
            "Native Stream Dock hotkey settings do not dispatch for custom code actions; use the local WTDeck companion sender."
        }
      }
    }
  };
})();
