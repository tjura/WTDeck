(function () {
  let socket = null;
  let context = null;
  let actionUuid = null;
  let settings = {};

  const adapterInput = document.getElementById("adapter");
  const hotkeyInput = document.getElementById("hotkey");
  const companionUrlInput = document.getElementById("companionUrl");
  const invertTelemetryInput = document.getElementById("invertTelemetry");

  [adapterInput, hotkeyInput, companionUrlInput, invertTelemetryInput].forEach((input) => {
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
    const defaultHotkey = actionDefinition && actionDefinition.command
      ? actionDefinition.command.defaultHotkeyLabel
      : "";
    adapterInput.value = normalizeAdapter(settings.commandAdapter);
    hotkeyInput.value = settings.hotkeyLabel || "";
    hotkeyInput.placeholder = defaultHotkey ? "Default: " + defaultHotkey : "Default action binding";
    companionUrlInput.value = settings.companionUrl || "";
    invertTelemetryInput.checked = Boolean(settings.invertTelemetry);
  }

  function saveSettings() {
    settings = {
      commandAdapter: normalizeAdapter(adapterInput.value),
      hotkeyLabel: hotkeyInput.value.trim(),
      companionUrl: companionUrlInput.value.trim(),
      invertTelemetry: invertTelemetryInput.checked
    };
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

  function currentActionDefinition() {
    const config = window.WTDECK_ACTION_CONFIG;
    if (!config || !config.actions || !actionUuid) {
      return null;
    }
    return config.actions[actionUuid] || null;
  }
})();
