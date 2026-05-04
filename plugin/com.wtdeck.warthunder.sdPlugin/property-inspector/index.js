(function () {
  let socket = null;
  let context = null;
  let actionUuid = null;
  let settings = {};
  let bindingLookupKey = "";
  let bindingLookupToken = 0;
  let brakeBindingLookupKey = "";
  let brakeBindingLookupToken = 0;

  const adapterField = document.getElementById("adapterField");
  const adapterInput = document.getElementById("adapter");
  const hotkeyField = document.getElementById("hotkeyField");
  const hotkeyLabel = document.getElementById("hotkeyLabel");
  const hotkeyInput = document.getElementById("hotkey");
  const bindingStatus = document.getElementById("bindingStatus");
  const autoLandingAssistInput = document.getElementById("autoLandingAssist");
  const autoLandingAssistField = document.getElementById("autoLandingAssistField");
  const autoBrakeHotkeyInput = document.getElementById("autoBrakeHotkey");
  const autoBrakeHotkeyField = document.getElementById("autoBrakeHotkeyField");
  const autoBrakeHotkeyLabel = document.getElementById("autoBrakeHotkeyLabel");
  const autoBrakeBindingStatus = document.getElementById("autoBrakeBindingStatus");
  const companionUrlField = document.getElementById("companionUrlField");
  const companionUrlInput = document.getElementById("companionUrl");
  const invertTelemetryInput = document.getElementById("invertTelemetry");
  const invertTelemetryField = document.getElementById("invertTelemetryField");
  const alertSoundsInput = document.getElementById("alertSounds");
  const alertSoundsField = document.getElementById("alertSoundsField");
  const alertSoundsLabel = document.getElementById("alertSoundsLabel");
  const settingsNote = document.getElementById("settingsNote");

  [
    adapterInput,
    hotkeyInput,
    autoLandingAssistInput,
    autoBrakeHotkeyInput,
    companionUrlInput,
    invertTelemetryInput,
    alertSoundsInput
  ].forEach((input) => {
    input.addEventListener("change", saveSettings);
    input.addEventListener("input", saveSettings);
  });

  window.connectElgatoStreamDeckSocket = function (
    port,
    propertyInspectorUuid,
    registerEvent,
    _info,
    actionInfo
  ) {
    const parsedActionInfo = safeJson(actionInfo) || {};
    context = parsedActionInfo.context;
    actionUuid = parsedActionInfo.action || null;
    settings = parsedActionInfo.payload && parsedActionInfo.payload.settings
      ? parsedActionInfo.payload.settings
      : {};
    applySettings();

    socket = new WebSocket("ws://127.0.0.1:" + port);
    socket.onopen = function () {
      send({
        event: registerEvent,
        uuid: propertyInspectorUuid
      });
      send({
        event: "getSettings",
        context: context
      });
    };
    socket.onmessage = function (message) {
      const event = safeJson(message.data);
      if (event && event.event === "didReceiveSettings") {
        settings = event.payload && event.payload.settings ? event.payload.settings : {};
        applySettings();
      }
    };
  };

  function applySettings() {
    const actionDefinition = currentActionDefinition();
    const commandSupported = supportsCommand(actionDefinition);
    const alertSoundConfig = soundAlertConfig(actionDefinition);
    const alertSoundsSupported = Boolean(alertSoundConfig);
    const autoAssistConfig = autoLandingAssistConfig(actionDefinition);
    const autoAssistSupported = Boolean(autoAssistConfig);
    const autoAssistEnabled = autoAssistSupported && settings[autoAssistConfig.settingKey] === true;
    const defaultHotkey = actionDefinition && actionDefinition.command
      ? actionDefinition.command.defaultHotkeyLabel
      : "";
    const defaultBrakeHotkey = autoAssistConfig ? autoAssistConfig.defaultBrakeHotkeyLabel || "" : "";
    adapterField.style.display = commandSupported ? "grid" : "none";
    hotkeyField.style.display = commandSupported ? "grid" : "none";
    companionUrlField.style.display = commandSupported ? "grid" : "none";
    adapterInput.value = normalizeAdapter(settings.commandAdapter);
    hotkeyLabel.textContent = primaryBindingLabel(actionDefinition);
    hotkeyInput.value = settings.hotkeyLabel || "";
    hotkeyInput.placeholder = primaryBindingPlaceholder(actionDefinition, defaultHotkey);
    autoLandingAssistInput.checked = autoAssistEnabled;
    autoLandingAssistField.style.display = autoAssistSupported ? "grid" : "none";
    autoBrakeHotkeyLabel.textContent = brakeBindingLabel(actionDefinition);
    autoBrakeHotkeyInput.value = autoAssistConfig ? settings[autoAssistConfig.brakeBindingSettingKey] || "" : "";
    autoBrakeHotkeyInput.placeholder = brakeBindingPlaceholder(defaultBrakeHotkey);
    autoBrakeHotkeyField.style.display = autoAssistEnabled ? "grid" : "none";
    companionUrlInput.value = settings.companionUrl || "";
    invertTelemetryInput.checked = Boolean(settings.invertTelemetry);
    invertTelemetryField.style.display = supportsTelemetryInversion(actionDefinition) ? "grid" : "none";
    alertSoundsInput.checked = alertSoundsSupported && alertSoundsEnabled(alertSoundConfig);
    alertSoundsField.style.display = alertSoundsSupported ? "grid" : "none";
    alertSoundsLabel.textContent = alertSoundsSupported ? alertSoundConfig.label : "Alert sounds";
    updateSettingsNote(commandSupported, alertSoundConfig, autoAssistEnabled);
    scheduleBindingAutoFill(actionDefinition);
    scheduleBrakeBindingAutoFill(actionDefinition, autoAssistConfig, autoAssistEnabled);
  }

  function saveSettings() {
    const actionDefinition = currentActionDefinition();
    settings = {};
    if (supportsCommand(actionDefinition)) {
      settings.commandAdapter = normalizeAdapter(adapterInput.value);
      settings.hotkeyLabel = hotkeyInput.value.trim();
      settings.companionUrl = companionUrlInput.value.trim();
    }
    const autoAssistConfig = autoLandingAssistConfig(actionDefinition);
    if (autoAssistConfig) {
      settings[autoAssistConfig.settingKey] = autoLandingAssistInput.checked;
      settings[autoAssistConfig.brakeBindingSettingKey] = autoBrakeHotkeyInput.value.trim();
    }
    if (supportsTelemetryInversion(actionDefinition)) {
      settings.invertTelemetry = invertTelemetryInput.checked;
    }
    const alertSoundConfig = soundAlertConfig(actionDefinition);
    if (alertSoundConfig) {
      settings[alertSoundConfig.settingKey] = alertSoundsInput.checked;
    }
    send({
      event: "setSettings",
      context: context,
      payload: settings
    });
  }

  function send(payload) {
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return;
    }
    socket.send(JSON.stringify(payload));
  }

  function safeJson(value) {
    if (!value) {
      return null;
    }
    if (typeof value === "object") {
      return value;
    }
    try {
      return JSON.parse(value);
    } catch (_error) {
      return null;
    }
  }

  function normalizeAdapter(adapter) {
    if (adapter === "companion-http" || adapter === "none") {
      return adapter;
    }
    return "companion-http";
  }

  function primaryBindingLabel(actionDefinition) {
    if (!actionDefinition) {
      return "Game Binding";
    }
    if (actionDefinition.id === "drogueChute") {
      return "Drogue Deploy Binding";
    }
    if (actionDefinition.id === "airbrake") {
      return "Air Brake Binding";
    }
    if (actionDefinition.id === "gear") {
      return "Landing Gear Binding";
    }
    if (actionDefinition.id === "flapsUp") {
      return "Flaps Up Binding";
    }
    if (actionDefinition.id === "flapsDown") {
      return "Flaps Down Binding";
    }
    if (actionDefinition.id === "flares") {
      return "Fire Flares Binding";
    }
    if (actionDefinition.id === "chaff") {
      return "Fire Chaff Binding";
    }
    return "Game Binding";
  }

  function primaryBindingPlaceholder(actionDefinition, defaultHotkey) {
    if (!defaultHotkey) {
      return "Set War Thunder binding";
    }
    const label = primaryBindingLabel(actionDefinition).replace(" Binding", "");
    return "Default " + label + ": " + defaultHotkey;
  }

  function brakeBindingLabel(actionDefinition) {
    if (actionDefinition && actionDefinition.id === "drogueChute") {
      return "Wheel Brake Hold Binding";
    }
    return "Wheel Brake Binding";
  }

  function brakeBindingPlaceholder(defaultBrakeHotkey) {
    return defaultBrakeHotkey
      ? "Default Brake Hold: " + defaultBrakeHotkey
      : "Set War Thunder brake hold binding";
  }

  function scheduleBindingAutoFill(actionDefinition) {
    if (!bindingStatus) {
      return;
    }
    if (!supportsCommand(actionDefinition)) {
      updateBindingStatus("No game binding needed.", "");
      return;
    }
    if (hotkeyInput.value.trim()) {
      updateBindingStatus("Configured binding.", "");
      return;
    }
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      updateBindingStatus("", "");
      return;
    }

    const url = companionBindingUrl(null);
    if (!url) {
      updateBindingStatus("Companion URL is invalid.", "warn");
      return;
    }

    const lookupKey = actionUuid + "|" + url;
    if (lookupKey === bindingLookupKey) {
      return;
    }
    bindingLookupKey = lookupKey;

    const token = bindingLookupToken + 1;
    bindingLookupToken = token;
    updateBindingStatus("Checking War Thunder binding...", "");

    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), 1000);
    fetch(url, {
      method: "GET",
      signal: controller.signal
    })
      .then((response) => response.json())
      .then((result) => {
        if (token !== bindingLookupToken) {
          return;
        }
        if (!result || !result.ok || !result.hotkeyLabel) {
          updateBindingStatus("No keyboard binding found.", "warn");
          return;
        }
        if (hotkeyInput.value.trim() || (settings.hotkeyLabel || "").trim()) {
          updateBindingStatus("Configured binding.", "");
          return;
        }
        hotkeyInput.value = result.hotkeyLabel;
        updateBindingStatus(bindingStatusMessage(result), result.source === "wtdeck-default" ? "warn" : "ok");
        saveSettings();
      })
      .catch(() => {
        if (token === bindingLookupToken) {
          updateBindingStatus("Companion unavailable.", "warn");
        }
      })
      .finally(() => window.clearTimeout(timeoutId));
  }

  function scheduleBrakeBindingAutoFill(actionDefinition, autoAssistConfig, autoAssistEnabled) {
    if (!autoBrakeBindingStatus) {
      return;
    }
    if (!autoAssistConfig || !autoAssistEnabled) {
      updateBrakeBindingStatus("", "");
      return;
    }
    const currentBrakeHotkey = autoBrakeHotkeyInput.value.trim();
    const defaultBrakeHotkey = (autoAssistConfig.defaultBrakeHotkeyLabel || "").trim();
    if (currentBrakeHotkey && currentBrakeHotkey !== defaultBrakeHotkey) {
      updateBrakeBindingStatus("Configured brake binding.", "");
      return;
    }
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      updateBrakeBindingStatus("", "");
      return;
    }

    const url = companionBindingUrl({
      controlId: autoAssistConfig.brakeWarThunderControlId,
      controlIds: autoAssistConfig.brakeWarThunderControlIds,
      defaultHotkeyLabel: autoAssistConfig.defaultBrakeHotkeyLabel,
      intent: autoAssistConfig.brakeIntent
    });
    if (!url) {
      updateBrakeBindingStatus("Companion URL is invalid.", "warn");
      return;
    }

    const lookupKey = actionUuid + "|brake|" + url;
    if (lookupKey === brakeBindingLookupKey) {
      return;
    }
    brakeBindingLookupKey = lookupKey;

    const token = brakeBindingLookupToken + 1;
    brakeBindingLookupToken = token;
    updateBrakeBindingStatus("Checking brake binding...", "");

    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), 1000);
    fetch(url, {
      method: "GET",
      signal: controller.signal
    })
      .then((response) => response.json())
      .then((result) => {
        if (token !== brakeBindingLookupToken) {
          return;
        }
        if (!result || !result.ok || !result.hotkeyLabel) {
          updateBrakeBindingStatus("No brake binding found.", "warn");
          return;
        }
        const currentBrakeHotkey = autoBrakeHotkeyInput.value.trim();
        const defaultBrakeHotkey = (autoAssistConfig.defaultBrakeHotkeyLabel || "").trim();
        if (currentBrakeHotkey && currentBrakeHotkey !== defaultBrakeHotkey) {
          updateBrakeBindingStatus("Configured brake binding.", "");
          return;
        }
        autoBrakeHotkeyInput.value = result.hotkeyLabel;
        updateBrakeBindingStatus(bindingStatusMessage(result), result.source === "wtdeck-default" ? "warn" : "ok");
        saveSettings();
      })
      .catch(() => {
        if (token === brakeBindingLookupToken) {
          updateBrakeBindingStatus("Companion unavailable.", "warn");
        }
      })
      .finally(() => window.clearTimeout(timeoutId));
  }

  function companionBindingUrl(bindingOverride) {
    const defaults = window.WTDECK_DEFAULTS || {};
    const commands = defaults.commands || {};
    const commandUrl = companionUrlInput.value.trim() || commands.companionUrl || "";
    if (!commandUrl || !actionUuid) {
      return null;
    }
    try {
      const url = new URL(commandUrl);
      url.pathname = "/bindings";
      url.search = "";
      url.searchParams.set("actionUuid", actionUuid);
      if (bindingOverride) {
        if (Array.isArray(bindingOverride.controlIds) && bindingOverride.controlIds.length > 0) {
          url.searchParams.set("controlIds", bindingOverride.controlIds.join(","));
        }
        if (bindingOverride.controlId) {
          url.searchParams.set("controlId", bindingOverride.controlId);
        }
        if (bindingOverride.defaultHotkeyLabel) {
          url.searchParams.set("defaultHotkeyLabel", bindingOverride.defaultHotkeyLabel);
        }
        if (bindingOverride.intent) {
          url.searchParams.set("intent", bindingOverride.intent);
        }
      }
      return url.toString();
    } catch (_error) {
      return null;
    }
  }

  function bindingStatusMessage(result) {
    if (result.source === "war-thunder-machine") {
      return "Detected from War Thunder: " + result.hotkeyLabel;
    }
    if (result.source === "wtdeck-default") {
      return "Using WTDeck default: " + result.hotkeyLabel;
    }
    return "Detected binding: " + result.hotkeyLabel;
  }

  function updateBindingStatus(message, tone) {
    bindingStatus.textContent = message;
    if (tone) {
      bindingStatus.dataset.tone = tone;
    } else {
      delete bindingStatus.dataset.tone;
    }
  }

  function updateBrakeBindingStatus(message, tone) {
    autoBrakeBindingStatus.textContent = message;
    if (tone) {
      autoBrakeBindingStatus.dataset.tone = tone;
    } else {
      delete autoBrakeBindingStatus.dataset.tone;
    }
  }

  function currentActionDefinition() {
    const config = window.WTDECK_ACTION_CONFIG;
    if (!config || !config.actions || !actionUuid) {
      return null;
    }
    return config.actions[actionUuid] || null;
  }

  function supportsTelemetryInversion(actionDefinition) {
    return Boolean(
      actionDefinition &&
      actionDefinition.kind === "toggle" &&
      actionDefinition.telemetry &&
      actionDefinition.telemetry.normalizedField
    );
  }

  function soundAlertConfig(actionDefinition) {
    if (!actionDefinition || !actionDefinition.alerts) {
      return null;
    }
    const alerts = actionDefinition.alerts;
    if (actionDefinition.id === "gForce" && (alerts.gWarning || alerts.gDanger || alerts.dangerSound)) {
      return {
        settingKey: "gAlertSoundsEnabled",
        legacySettingKey: "dangerSoundEnabled",
        label: "G warning sounds",
        note: "WTDeck plays local warning and danger tones while the G indicator is under high load."
      };
    }
    if (
      actionDefinition.id === "altitude" &&
      (alerts.groundCollisionWarning || alerts.groundCollisionDanger || alerts.groundCollisionPullUp)
    ) {
      return {
        settingKey: "groundCollisionSoundsEnabled",
        label: "Ground collision sounds",
        note: "WTDeck plays local GPWS-style terrain warning tones from the Altitude indicator."
      };
    }
    if (actionDefinition.id === "fuel" && alerts.lowFuelWarning) {
      return {
        settingKey: "fuelAlertSoundsEnabled",
        label: "Low fuel voice warning",
        note: "WTDeck says LOW FUEL every 10 seconds while the Fuel indicator is below 3 minutes until you press the button to acknowledge it."
      };
    }
    return null;
  }

  function autoLandingAssistConfig(actionDefinition) {
    const automation = actionDefinition && actionDefinition.automation
      ? actionDefinition.automation
      : null;
    return automation && automation.landingAssist ? automation.landingAssist : null;
  }

  function alertSoundsEnabled(alertSoundConfig) {
    if (!alertSoundConfig) {
      return false;
    }
    if (settings[alertSoundConfig.settingKey] !== undefined) {
      return settings[alertSoundConfig.settingKey] !== false;
    }
    if (
      alertSoundConfig.legacySettingKey &&
      settings[alertSoundConfig.legacySettingKey] !== undefined
    ) {
      return settings[alertSoundConfig.legacySettingKey] !== false;
    }
    return true;
  }

  function supportsCommand(actionDefinition) {
    return Boolean(
      actionDefinition &&
      actionDefinition.command &&
      actionDefinition.command.intent &&
      actionDefinition.command.intent !== "none"
    );
  }

  function updateSettingsNote(commandSupported, alertSoundConfig, autoAssistEnabled) {
    if (!settingsNote) {
      return;
    }
    if (autoAssistEnabled) {
      settingsNote.textContent =
        "Auto landing assist arms on tap, holds wheel brake after touchdown, and deploys Drogue when speed is in range.";
      settingsNote.style.display = "block";
      return;
    }
    if (commandSupported) {
      settingsNote.textContent =
        "WTDeck sends the focused game window the configured key through the local WTDeck key sender.";
      settingsNote.style.display = "block";
      return;
    }
    if (alertSoundConfig) {
      settingsNote.textContent = alertSoundConfig.note;
      settingsNote.style.display = "block";
      return;
    }
    settingsNote.textContent = "";
    settingsNote.style.display = "none";
  }
})();
