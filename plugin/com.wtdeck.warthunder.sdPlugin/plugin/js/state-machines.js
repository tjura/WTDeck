(function () {
  function classifyPercent(percent, thresholds) {
    if (percent === null || percent === undefined || Number.isNaN(percent)) {
      return "unknown";
    }
    const offMax = thresholds && Number.isFinite(thresholds.offMax) ? thresholds.offMax : 5;
    const onMin = thresholds && Number.isFinite(thresholds.onMin) ? thresholds.onMin : 95;
    if (percent <= offMax) {
      return "off";
    }
    if (percent >= onMin) {
      return "on";
    }
    return "moving";
  }

  function modelForAction(actionDefinition, telemetry, settings) {
    const actionId = actionDefinition.id;
    const connected = Boolean(telemetry && telemetry.connected);
    const valid = Boolean(telemetry && telemetry.valid);
    const inverted = Boolean(settings && settings.invertTelemetry);

    if (!connected || !valid) {
      return {
        actionId: actionId,
        connected: connected,
        statusKey: "unknown",
        statusText: connected ? "NO FLIGHT" : "OFFLINE",
        valueText: "",
        percent: null,
        tone: "offline"
      };
    }

    const fieldName = actionDefinition.telemetry.normalizedField;
    let percent = telemetry[fieldName];
    if (typeof percent === "number" && inverted) {
      percent = 100 - percent;
    }
    const stateKey = classifyPercent(percent, actionDefinition.thresholds);
    const stateText = stateTextFor(actionDefinition, stateKey, percent);

    return {
      actionId: actionId,
      connected: connected,
      statusKey: stateKey,
      statusText: stateText,
      valueText: percent === null ? "" : Math.round(percent) + "%",
      percent: percent,
      tone: toneForState(stateKey)
    };
  }

  function stateTextFor(actionDefinition, stateKey, percent) {
    if (stateKey === "moving" && actionDefinition.states.moving) {
      return actionDefinition.states.moving;
    }
    if (stateKey === "moving" && actionDefinition.states.partial) {
      return actionDefinition.states.partial;
    }
    if (stateKey === "unknown") {
      return actionDefinition.states.unknown || "UNKNOWN";
    }
    if (stateKey === "off") {
      return actionDefinition.states.off || "OFF";
    }
    if (stateKey === "on") {
      return actionDefinition.states.on || "ON";
    }
    if (typeof percent === "number") {
      return Math.round(percent) + "%";
    }
    return "UNKNOWN";
  }

  function toneForState(stateKey) {
    if (stateKey === "on") {
      return "safe";
    }
    if (stateKey === "moving") {
      return "transit";
    }
    if (stateKey === "off") {
      return "dark";
    }
    return "offline";
  }

  window.WTDeckStateMachines = {
    classifyPercent: classifyPercent,
    modelForAction: modelForAction
  };
})();
