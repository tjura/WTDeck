(function () {
  class ActionRuntime {
    constructor(bridge, actionConfig, defaults) {
      this.bridge = bridge;
      this.actionConfig = actionConfig;
      this.defaults = defaults;
      this.contexts = new Map();
      this.client = new window.WTDeckWarThunderClient(defaults.telemetry);
      this.telemetry = null;
      this.pollTimer = null;
      this.lastPollStartedAt = 0;
      this.audioAlerts = window.WTDeckAudioAlerts ? new window.WTDeckAudioAlerts() : null;
      this.lastAlertAtByAction = new Map();
      this.lastActiveFlight = null;
      this.beforeUnloadHandler = () => this.releaseAllAutoBrakes("unload");
    }

    start() {
      this.bridge.on("willAppear", (event) => this.onWillAppear(event));
      this.bridge.on("willDisappear", (event) => this.onWillDisappear(event));
      this.bridge.on("didReceiveSettings", (event) => this.onDidReceiveSettings(event));
      this.bridge.on("keyDown", (event) => this.onKeyDown(event));
      this.bridge.on("keyUp", (event) => this.onKeyUp(event));
      this.bridge.on("connected", () => this.bridge.logMessage("Plugin runtime connected."));
      window.addEventListener("beforeunload", this.beforeUnloadHandler);
      this.pollTimer = window.setInterval(
        () => this.pollTelemetry(),
        this.defaults.telemetry.pollIntervalMs
      );
      this.pollTelemetry();
    }

    onWillAppear(event) {
      if (!this.actionConfig.actions[event.action]) {
        return;
      }
      this.contexts.set(event.context, {
        contextId: event.context,
        actionUuid: event.action,
        settings: event.payload && event.payload.settings ? event.payload.settings : {},
        renderKey: "",
        model: null,
        baseModel: null,
        commandActive: false,
        autoAssist: newAutoAssistState(),
        fuelAlarm: newFuelAlarmState()
      });
      this.bridge.getSettings(event.context);
      this.renderContext(event.context);
    }

    onWillDisappear(event) {
      const context = this.contexts.get(event.context);
      if (context) {
        const actionDefinition = this.actionConfig.actions[context.actionUuid];
        this.disarmAutoAssist(actionDefinition, context, "disappear");
      }
      this.contexts.delete(event.context);
    }

    onDidReceiveSettings(event) {
      const context = this.contexts.get(event.context);
      if (!context) {
        return;
      }
      context.settings = event.payload && event.payload.settings ? event.payload.settings : {};
      const actionDefinition = this.actionConfig.actions[context.actionUuid];
      if (!autoAssistEnabled(actionDefinition, context.settings)) {
        this.disarmAutoAssist(actionDefinition, context, "settings");
      }
      context.renderKey = "";
      this.renderContext(event.context);
    }

    async onKeyDown(event) {
      const context = this.contexts.get(event.context);
      if (!context) {
        return;
      }
      const actionDefinition = this.actionConfig.actions[context.actionUuid];
      if (fuelAlarmAction(actionDefinition)) {
        if (isActiveFlightTelemetry(this.telemetry)) {
          this.acknowledgeFuelAlarm(context);
        }
        return;
      }
      if (autoAssistEnabled(actionDefinition, context.settings)) {
        const handled = await this.toggleAutoAssist(actionDefinition, context);
        if (!handled) {
          this.bridge.logMessage("Auto landing assist failed for action '" + context.actionUuid + "'.");
        }
        return;
      }
      const handled = await this.dispatchCommand(actionDefinition, context.settings, "down", context);
      if (!handled) {
        this.bridge.logMessage("Command failed for action '" + context.actionUuid + "'.");
      }
    }

    async onKeyUp(event) {
      const context = this.contexts.get(event.context);
      if (!context) {
        return;
      }
      const actionDefinition = this.actionConfig.actions[context.actionUuid];
      if (autoAssistEnabled(actionDefinition, context.settings)) {
        return;
      }
      const handled = await this.dispatchCommand(actionDefinition, context.settings, "up", context);
      if (!handled) {
        this.bridge.logMessage("Command release failed for action '" + context.actionUuid + "'.");
      }
    }

    async pollTelemetry() {
      if (Date.now() - this.lastPollStartedAt < this.defaults.telemetry.pollIntervalMs / 2) {
        return;
      }
      this.lastPollStartedAt = Date.now();
      this.telemetry = await this.client.readSnapshot();
      this.handleFlightActivityTransition();
      await this.updateAutoAssist();
      this.renderAll();
    }

    handleFlightActivityTransition() {
      const activeFlight = isActiveFlightTelemetry(this.telemetry);
      if (!activeFlight && this.lastActiveFlight !== false) {
        this.stopInactiveFlightEffects();
      }
      this.lastActiveFlight = activeFlight;
    }

    stopInactiveFlightEffects() {
      this.lastAlertAtByAction.clear();
      this.contexts.forEach((context) => {
        context.fuelAlarm = newFuelAlarmState();
      });
      if (this.audioAlerts && this.audioAlerts.stopAll) {
        this.audioAlerts.stopAll();
      }
    }

    renderAll() {
      this.contexts.forEach((_value, context) => this.renderContext(context));
    }

    renderContext(contextId) {
      const context = this.contexts.get(contextId);
      if (!context) {
        return;
      }
      const actionDefinition = this.actionConfig.actions[context.actionUuid];
      const baseModel = window.WTDeckStateMachines.modelForAction(
        actionDefinition,
        this.telemetry,
        context.settings
      );
      context.baseModel = baseModel;
      const assistedModel = displayModelForAutoAssist(actionDefinition, context, baseModel);
      const model = displayModelForFuelAlarm(actionDefinition, context, assistedModel);
      context.model = model;
      this.maybePlayActionAlert(context.actionUuid, actionDefinition, model, context.settings);
      const renderKey = JSON.stringify({ action: context.actionUuid, model: model });
      if (renderKey === context.renderKey) {
        return;
      }
      context.renderKey = renderKey;
      this.bridge.setTitle(contextId, "");
      this.bridge.setImage(contextId, window.WTDeckKeyRenderer.render(actionDefinition, model));
    }

    async dispatchCommand(actionDefinition, settings, phase, context) {
      if (isReadOnlyAction(actionDefinition)) {
        return true;
      }
      const command = actionDefinition.command || {};
      if (!command.intent || command.intent === "none") {
        return true;
      }
      const adapter = normalizeAdapter(
        settings.commandAdapter || command.adapter || this.defaults.commands.defaultAdapter
      );
      if (adapter === "none") {
        return true;
      }
      if (phase !== "up" && !isActiveFlightTelemetry(this.telemetry)) {
        this.bridge.logMessage(
          "Command '" + command.intent + "' ignored because War Thunder is not in an active flight."
        );
        return true;
      }
      if (command.requiresReadyState && phase === "down" && !isCommandModelReady(context && context.model)) {
        this.bridge.logMessage(
          "Command '" + command.intent + "' ignored because action state is '" +
          commandStateLabel(context && context.model) + "'."
        );
        return true;
      }
      if (command.requiresReadyState && phase === "up" && context && !context.commandActive) {
        return true;
      }
      let handled = false;
      if (adapter === "companion-http") {
        handled = await this.dispatchCompanionCommand(actionDefinition, settings, phase);
        if (command.requiresReadyState && context) {
          if (phase === "down" && handled) {
            context.commandActive = true;
          }
          if (phase === "up") {
            context.commandActive = false;
          }
        }
        return handled;
      }

      this.bridge.logMessage(
        "Command '" + command.intent + "' is not dispatched because adapter is '" + adapter + "'."
      );
      return false;
    }

    async dispatchCompanionCommand(actionDefinition, settings, phase) {
      const command = actionDefinition.command || {};
      const hotkeyLabel = effectiveHotkeyLabel(actionDefinition, settings);
      if (!hotkeyLabel) {
        this.bridge.logMessage(
          "Command '" + command.intent + "' has no game binding label configured."
        );
        return true;
      }
      return this.sendCompanionCommand(settings, command.intent, hotkeyLabel, phase);
    }

    async sendCompanionCommand(settings, intent, hotkeyLabel, phase, binding) {
      if (!hotkeyLabel) {
        this.bridge.logMessage("Command '" + intent + "' has no game binding label configured.");
        return true;
      }
      const url = settings.companionUrl || this.defaults.commands.companionUrl;
      const payload = {
        intent: intent,
        hotkeyLabel: hotkeyLabel,
        phase: phase || "tap",
        source: "streamdock",
        plugin: "com.wtdeck.warthunder"
      };
      if (binding && Array.isArray(binding.scanCodes) && binding.scanCodes.length > 0) {
        payload.scanCodes = binding.scanCodes;
      }
      const controller = new AbortController();
      const timeoutId = window.setTimeout(
        () => controller.abort(),
        this.defaults.commands.companionTimeoutMs
      );
      try {
        const response = await fetch(url, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
          signal: controller.signal
        });
        return response.ok;
      } catch (error) {
        this.bridge.logMessage("Companion command failed: " + error.message);
        return false;
      } finally {
        window.clearTimeout(timeoutId);
      }
    }

    async sendCompanionTap(settings, intent, hotkeyLabel, binding) {
      return this.sendCompanionCommand(settings, intent, hotkeyLabel, "tap", binding);
    }

    acknowledgeFuelAlarm(context) {
      const model = context.baseModel || context.model;
      if (model && model.lowFuelWarningActive) {
        context.fuelAlarm = context.fuelAlarm || newFuelAlarmState();
        context.fuelAlarm.acknowledgedWarningKey = fuelWarningKey(model);
        context.renderKey = "";
        this.renderContext(context.contextId);
      }
      return true;
    }

    async toggleAutoAssist(actionDefinition, context) {
      if (!isActiveFlightTelemetry(this.telemetry)) {
        await this.disarmAutoAssist(actionDefinition, context, "inactive flight");
        context.renderKey = "";
        this.renderContext(context.contextId);
        return true;
      }
      if (autoAssistArmed(context)) {
        await this.disarmAutoAssist(actionDefinition, context, "manual");
      } else {
        context.autoAssist = newAutoAssistState();
        context.autoAssist.armed = true;
        context.autoAssist.phase = "armed";
        context.autoAssist.armedAt = Date.now();
      }
      context.renderKey = "";
      this.renderContext(context.contextId);
      return true;
    }

    async updateAutoAssist() {
      for (const context of this.contexts.values()) {
        const actionDefinition = this.actionConfig.actions[context.actionUuid];
        if (!autoAssistEnabled(actionDefinition, context.settings)) {
          continue;
        }
        await this.updateAutoAssistContext(actionDefinition, context);
      }
    }

    async updateAutoAssistContext(actionDefinition, context) {
      const state = context.autoAssist || newAutoAssistState();
      context.autoAssist = state;
      if (!state.armed) {
        return;
      }

      const telemetry = this.telemetry || {};
      if (!isActiveFlightTelemetry(telemetry)) {
        await this.disarmAutoAssist(actionDefinition, context, "inactive flight");
        return;
      }

      const assist = landingAssistConfig(actionDefinition);
      const thresholds = assistThresholds(assist);
      const radarAltitudeMeters = numberOrNull(telemetry.radarAltitudeMeters);
      const iasKmh = numberOrNull(telemetry.iasKmh);
      const tasKmh = numberOrNull(telemetry.tasKmh);
      const speedKmh = maxNumber([iasKmh, tasKmh]);
      const gearPercent = numberOrNull(telemetry.gearPercent);
      const throttlePercent = numberOrNull(telemetry.throttlePercent);
      const verticalSpeedMps = numberOrDefault(telemetry.verticalSpeedMps, 0);
      const now = Number.isFinite(telemetry.readAt) ? telemetry.readAt : Date.now();

      if (
        state.brakeActive &&
        radarAltitudeMeters !== null &&
        radarAltitudeMeters > thresholds.airborneReleaseRadarAltitudeMeters
      ) {
        await this.disarmAutoAssist(actionDefinition, context, "airborne");
        return;
      }

      if (!state.gearSent && autoAssistGearReady(thresholds, gearPercent, speedKmh)) {
        const gearIntent = assist.gearIntent || "landing-gear-toggle";
        const gearBinding = await this.resolveAutoGearBinding(actionDefinition, context);
        const sent = await this.sendCompanionTap(
          context.settings,
          gearIntent,
          gearBinding.hotkeyLabel,
          gearBinding
        );
        if (sent) {
          state.gearSent = true;
          this.bridge.logMessage("Auto landing assist extended landing gear.");
        } else {
          this.bridge.logMessage("Auto landing assist failed to extend landing gear.");
        }
      }

      const touchdownCandidate = isTouchdownCandidate(
        thresholds,
        gearPercent,
        radarAltitudeMeters,
        iasKmh,
        speedKmh,
        throttlePercent,
        verticalSpeedMps
      );
      if (touchdownCandidate) {
        if (!state.touchdownCandidateSince) {
          state.touchdownCandidateSince = now;
          state.touchdownSamples = 0;
        }
        state.touchdownSamples += 1;
      } else {
        state.touchdownCandidateSince = 0;
        state.touchdownSamples = 0;
      }

      const touchdownConfirmed = touchdownCandidate &&
        state.touchdownSamples >= 2 &&
        now - state.touchdownCandidateSince >= thresholds.stableTouchdownMs;
      if (!touchdownConfirmed && !state.brakeActive) {
        state.phase = "armed";
        return;
      }

      if (touchdownConfirmed && !state.brakeActive) {
        const brakeBinding = await this.resolveAutoBrakeBinding(actionDefinition, context);
        const sent = await this.sendCompanionCommand(
          context.settings,
          assist.brakeIntent,
          brakeBinding.hotkeyLabel,
          "down",
          brakeBinding
        );
        if (!sent) {
          return;
        }
        state.brakeActive = true;
        state.brakeHotkeyLabel = brakeBinding.hotkeyLabel;
        state.brakeScanCodes = brakeBinding.scanCodes;
        state.phase = "brake";
      }

      const baseModel = window.WTDeckStateMachines.modelForAction(
        actionDefinition,
        this.telemetry,
        context.settings
      );
      if (
        state.brakeActive &&
        !state.drogueSent &&
        autoAssistDrogueReady(baseModel)
      ) {
        const command = actionDefinition.command || {};
        const drogueHotkeyLabel = effectiveHotkeyLabel(actionDefinition, context.settings);
        const sent = await this.sendCompanionTap(context.settings, command.intent, drogueHotkeyLabel);
        if (sent) {
          state.drogueSent = true;
          state.phase = "drogue";
        }
      }

      if (state.brakeActive && speedKmh !== null && speedKmh <= thresholds.stoppedSpeedKmh) {
        if (!state.stoppedSince) {
          state.stoppedSince = now;
        }
        if (now - state.stoppedSince >= thresholds.stoppedHoldMs) {
          await this.releaseAutoBrake(actionDefinition, context, "stopped");
          context.autoAssist = newAutoAssistState();
          context.autoAssist.phase = "stopped";
          context.autoAssist.stoppedDisplayUntil = Date.now() + 1600;
          context.renderKey = "";
        }
        return;
      }

      if (speedKmh === null || speedKmh > thresholds.stoppedSpeedKmh) {
        state.stoppedSince = 0;
      }
    }

    async disarmAutoAssist(actionDefinition, context, reason) {
      await this.releaseAutoBrake(actionDefinition, context, reason);
      context.autoAssist = newAutoAssistState();
      context.renderKey = "";
    }

    async releaseAutoBrake(actionDefinition, context, reason) {
      const state = context.autoAssist;
      if (!state || !state.brakeActive) {
        return true;
      }
      const assist = landingAssistConfig(actionDefinition);
      const brakeBinding = state.brakeHotkeyLabel
        ? { hotkeyLabel: state.brakeHotkeyLabel, scanCodes: state.brakeScanCodes || [] }
        : await this.resolveAutoBrakeBinding(actionDefinition, context);
      const sent = await this.sendCompanionCommand(
        context.settings,
        assist.brakeIntent,
        brakeBinding.hotkeyLabel,
        "up",
        brakeBinding
      );
      if (sent) {
        state.brakeActive = false;
        state.brakeHotkeyLabel = "";
        state.brakeScanCodes = [];
        this.bridge.logMessage("Auto landing assist released brake: " + reason + ".");
      }
      return sent;
    }

    async resolveAutoGearBinding(actionDefinition, context) {
      const assist = landingAssistConfig(actionDefinition);
      const defaultLabel = assist && assist.defaultGearHotkeyLabel
        ? String(assist.defaultGearHotkeyLabel).trim()
        : "";
      const detectedBinding = await this.lookupCompanionBinding(context, {
        controlId: assist && assist.gearWarThunderControlId,
        defaultHotkeyLabel: defaultLabel,
        intent: assist && assist.gearIntent ? assist.gearIntent : "landing-gear-toggle"
      });
      if (detectedBinding && detectedBinding.hotkeyLabel) {
        return detectedBinding;
      }
      return { hotkeyLabel: defaultLabel, scanCodes: [] };
    }

    async resolveAutoBrakeBinding(actionDefinition, context) {
      const assist = landingAssistConfig(actionDefinition);
      const settingKey = assist ? assist.brakeBindingSettingKey : "";
      const configuredLabel = settingKey && context.settings && context.settings[settingKey]
        ? String(context.settings[settingKey]).trim()
        : "";
      const defaultLabel = assist && assist.defaultBrakeHotkeyLabel
        ? String(assist.defaultBrakeHotkeyLabel).trim()
        : "";
      if (configuredLabel && configuredLabel !== defaultLabel) {
        return { hotkeyLabel: configuredLabel, scanCodes: [] };
      }

      const detectedBinding = await this.lookupCompanionBinding(context, {
        controlId: assist && assist.brakeWarThunderControlId,
        controlIds: assist && assist.brakeWarThunderControlIds,
        defaultHotkeyLabel: defaultLabel,
        intent: assist && assist.brakeIntent
      });
      if (detectedBinding && detectedBinding.hotkeyLabel) {
        return detectedBinding;
      }
      return { hotkeyLabel: configuredLabel || defaultLabel, scanCodes: [] };
    }

    async lookupCompanionBinding(context, bindingOverride) {
      const url = companionBindingUrl(
        context.settings,
        this.defaults,
        context.actionUuid,
        bindingOverride
      );
      if (!url) {
        return "";
      }

      const controller = new AbortController();
      const timeoutId = window.setTimeout(
        () => controller.abort(),
        this.defaults.commands.companionTimeoutMs
      );
      try {
        const response = await fetch(url, {
          method: "GET",
          signal: controller.signal
        });
        if (!response.ok) {
          return null;
        }
        const result = await response.json();
        if (!result || !result.ok || !result.hotkeyLabel) {
          return null;
        }
        return {
          hotkeyLabel: String(result.hotkeyLabel).trim(),
          scanCodes: Array.isArray(result.scanCodes)
            ? result.scanCodes.map((code) => Number(code)).filter((code) => Number.isFinite(code))
            : []
        };
      } catch (_error) {
        return null;
      } finally {
        window.clearTimeout(timeoutId);
      }
    }

    releaseAllAutoBrakes(reason) {
      this.contexts.forEach((context) => {
        const actionDefinition = this.actionConfig.actions[context.actionUuid];
        this.releaseAutoBrake(actionDefinition, context, reason);
      });
    }

    maybePlayActionAlert(actionUuid, actionDefinition, model, settings) {
      if (!this.audioAlerts || !alertSoundsEnabled(actionDefinition, settings)) {
        return;
      }
      if (!isActiveFlightTelemetry(this.telemetry)) {
        return;
      }

      const alert = alertForModel(actionDefinition, model);
      if (!alert) {
        return;
      }

      const now = Date.now();
      const throttleKey = actionUuid + ":" + alert.stateKey;
      const lastPlayedAt = this.lastAlertAtByAction.get(throttleKey) || 0;
      if (now - lastPlayedAt < alert.cooldownMs) {
        return;
      }

      this.lastAlertAtByAction.set(throttleKey, now);
      this.audioAlerts.playAlert(alert.pattern, alert.options);
    }
  }

  function normalizeAdapter(adapter) {
    if (adapter === "companion-http" || adapter === "none") {
      return adapter;
    }
    return "none";
  }

  function isReadOnlyAction(actionDefinition) {
    return Boolean(
      actionDefinition &&
      (actionDefinition.kind === "indicator" || actionDefinition.kind === "readout")
    );
  }

  function newAutoAssistState() {
    return {
      armed: false,
      phase: "off",
      brakeActive: false,
      brakeHotkeyLabel: "",
      brakeScanCodes: [],
      gearSent: false,
      drogueSent: false,
      touchdownCandidateSince: 0,
      touchdownSamples: 0,
      stoppedSince: 0,
      stoppedDisplayUntil: 0,
      armedAt: 0
    };
  }

  function newFuelAlarmState() {
    return {
      acknowledgedWarningKey: ""
    };
  }

  function landingAssistConfig(actionDefinition) {
    const automation = actionDefinition && actionDefinition.automation
      ? actionDefinition.automation
      : null;
    return automation && automation.landingAssist ? automation.landingAssist : null;
  }

  function autoAssistEnabled(actionDefinition, settings) {
    const assist = landingAssistConfig(actionDefinition);
    return Boolean(
      actionDefinition &&
      actionDefinition.id === "drogueChute" &&
      assist &&
      settings &&
      settings[assist.settingKey] === true
    );
  }

  function autoAssistArmed(context) {
    const state = context && context.autoAssist;
    return Boolean(state && (state.armed || state.brakeActive));
  }

  function isActiveFlightTelemetry(telemetry) {
    return Boolean(telemetry && telemetry.activeFlight);
  }

  function displayModelForAutoAssist(actionDefinition, context, baseModel) {
    if (!autoAssistEnabled(actionDefinition, context.settings)) {
      return baseModel;
    }
    if (!baseModel || baseModel.connected === false) {
      return baseModel;
    }

    const state = context.autoAssist || {};
    const now = Date.now();
    const showingStopped = state.stoppedDisplayUntil && state.stoppedDisplayUntil > now;
    if (baseModel.statusKey === "unknown" && !state.armed && !showingStopped) {
      return baseModel;
    }
    if (showingStopped) {
      return autoAssistStatusModel(actionDefinition, baseModel, "stopped", "safe");
    }
    if (!state.armed) {
      return baseModel;
    }
    if (state.phase === "drogue") {
      return autoAssistStatusModel(actionDefinition, baseModel, "drogue", "safe");
    }
    if (state.phase === "brake") {
      return autoAssistStatusModel(actionDefinition, baseModel, "brake", "warning");
    }
    return autoAssistStatusModel(actionDefinition, baseModel, "armed", "warning");
  }

  function autoAssistStatusModel(actionDefinition, baseModel, stateKey, tone) {
    const states = actionDefinition.states || {};
    const model = Object.assign({}, baseModel);
    model.statusKey = stateKey;
    model.statusText = states[stateKey] || stateKey.toUpperCase();
    model.tone = tone;
    model.commandReady = false;
    return model;
  }

  function fuelAlarmAction(actionDefinition) {
    return Boolean(actionDefinition && actionDefinition.id === "fuel");
  }

  function displayModelForFuelAlarm(actionDefinition, context, baseModel) {
    if (!fuelAlarmAction(actionDefinition)) {
      return baseModel;
    }
    const state = context.fuelAlarm || newFuelAlarmState();
    context.fuelAlarm = state;
    if (!baseModel || !baseModel.lowFuelWarningActive) {
      state.acknowledgedWarningKey = "";
      return baseModel;
    }

    const warningKey = fuelWarningKey(baseModel);
    if (state.acknowledgedWarningKey !== warningKey) {
      state.acknowledgedWarningKey = "";
      return baseModel;
    }

    const states = actionDefinition.states || {};
    const model = Object.assign({}, baseModel);
    model.statusKey = "acknowledged";
    model.statusText = states.acknowledged || "ACK";
    model.tone = "warning";
    model.alertAcknowledged = true;
    return model;
  }

  function fuelWarningKey(model) {
    if (!model) {
      return "";
    }
    if (model.lowFuelWarningKey) {
      return String(model.lowFuelWarningKey);
    }
    return String(model.fuelSessionId || 0) + ":active";
  }

  function assistThresholds(assist) {
    const thresholds = assist && assist.thresholds ? assist.thresholds : {};
    return {
      gearDownMinPercent: thresholdNumber(thresholds, "gearDownMinPercent", 95),
      gearUpMaxPercent: thresholdNumber(thresholds, "gearUpMaxPercent", 5),
      autoGearMaxSpeedKmh: thresholdNumber(thresholds, "autoGearMaxSpeedKmh", 350),
      touchdownRadarAltitudeMeters: thresholdNumber(thresholds, "touchdownRadarAltitudeMeters", 2),
      touchdownMaxIasKmh: thresholdNumber(thresholds, "touchdownMaxIasKmh", 380),
      noRadarTouchdownMaxSpeedKmh: thresholdNumber(thresholds, "noRadarTouchdownMaxSpeedKmh", 260),
      noRadarSpeedOnlyTouchdownMaxSpeedKmh: thresholdNumber(thresholds, "noRadarSpeedOnlyTouchdownMaxSpeedKmh", 140),
      noRadarIdleThrottleMaxPercent: thresholdNumber(thresholds, "noRadarIdleThrottleMaxPercent", 5),
      stableTouchdownMs: thresholdNumber(thresholds, "stableTouchdownMs", 500),
      stableVerticalSpeedAbsMps: thresholdNumber(thresholds, "stableVerticalSpeedAbsMps", 2),
      airborneReleaseRadarAltitudeMeters: thresholdNumber(thresholds, "airborneReleaseRadarAltitudeMeters", 5),
      stoppedSpeedKmh: thresholdNumber(thresholds, "stoppedSpeedKmh", 8),
      stoppedHoldMs: thresholdNumber(thresholds, "stoppedHoldMs", 1000)
    };
  }

  function autoAssistGearReady(thresholds, gearPercent, speedKmh) {
    return gearPercent !== null &&
      gearPercent <= thresholds.gearUpMaxPercent &&
      speedKmh !== null &&
      speedKmh <= thresholds.autoGearMaxSpeedKmh;
  }

  function isTouchdownCandidate(
    thresholds,
    gearPercent,
    radarAltitudeMeters,
    iasKmh,
    speedKmh,
    throttlePercent,
    verticalSpeedMps
  ) {
    const radarTouchdown = radarAltitudeMeters !== null &&
      radarAltitudeMeters <= thresholds.touchdownRadarAltitudeMeters;
    const throttleIdle = throttlePercent !== null &&
      throttlePercent <= thresholds.noRadarIdleThrottleMaxPercent;
    const noRadarRolloutTouchdown = radarAltitudeMeters === null &&
      speedKmh !== null &&
      (
        (
          throttleIdle &&
          speedKmh <= thresholds.noRadarTouchdownMaxSpeedKmh
        ) ||
        speedKmh <= thresholds.noRadarSpeedOnlyTouchdownMaxSpeedKmh
      );
    return gearPercent !== null &&
      gearPercent >= thresholds.gearDownMinPercent &&
      iasKmh !== null &&
      iasKmh <= thresholds.touchdownMaxIasKmh &&
      (radarTouchdown || noRadarRolloutTouchdown) &&
      Math.abs(numberOrDefault(verticalSpeedMps, 0)) <= thresholds.stableVerticalSpeedAbsMps;
  }

  function autoAssistDrogueReady(baseModel) {
    return Boolean(baseModel && baseModel.commandReady === true);
  }

  function companionBindingUrl(settings, defaults, actionUuid, bindingOverride) {
    const commandUrl = settings && settings.companionUrl
      ? settings.companionUrl
      : defaults.commands.companionUrl;
    if (!commandUrl || !actionUuid) {
      return "";
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
      return "";
    }
  }

  function isCommandModelReady(model) {
    return Boolean(model && (model.commandReady === true || model.statusKey === "on"));
  }

  function commandStateLabel(model) {
    if (!model) {
      return "unknown";
    }
    return model.statusText || model.statusKey || "unknown";
  }

  function effectiveHotkeyLabel(actionDefinition, settings) {
    const configuredLabel = settings && settings.hotkeyLabel
      ? String(settings.hotkeyLabel).trim()
      : "";
    if (configuredLabel) {
      return configuredLabel;
    }
    const command = actionDefinition.command || {};
    return command.defaultHotkeyLabel ? String(command.defaultHotkeyLabel).trim() : "";
  }

  function alertSoundsEnabled(actionDefinition, settings) {
    if (!actionDefinition || !actionDefinition.alerts) {
      return false;
    }
    if (actionDefinition.id === "gForce") {
      return gAlertSoundsEnabled(actionDefinition, settings);
    }
    if (actionDefinition.id === "altitude") {
      return groundCollisionSoundsEnabled(actionDefinition, settings);
    }
    if (actionDefinition.id === "fuel") {
      return fuelAlertSoundsEnabled(actionDefinition, settings);
    }
    return false;
  }

  function gAlertSoundsEnabled(actionDefinition, settings) {
    const alerts = actionDefinition ? actionDefinition.alerts : null;
    const hasAlertConfig = Boolean(
      alerts &&
      (alerts.gWarning || alerts.gDanger || alerts.dangerSound)
    );
    if (!hasAlertConfig) {
      return false;
    }
    if (settings && settings.gAlertSoundsEnabled !== undefined) {
      return settings.gAlertSoundsEnabled !== false;
    }
    return !settings || settings.dangerSoundEnabled !== false;
  }

  function groundCollisionSoundsEnabled(actionDefinition, settings) {
    const alerts = actionDefinition ? actionDefinition.alerts : null;
    const hasAlertConfig = Boolean(
      alerts &&
      (alerts.groundCollisionWarning ||
        alerts.groundCollisionDanger ||
        alerts.groundCollisionPullUp)
    );
    if (!hasAlertConfig) {
      return false;
    }
    if (settings && settings.groundCollisionSoundsEnabled !== undefined) {
      return settings.groundCollisionSoundsEnabled !== false;
    }
    return true;
  }

  function fuelAlertSoundsEnabled(actionDefinition, settings) {
    const alerts = actionDefinition ? actionDefinition.alerts : null;
    const hasAlertConfig = Boolean(alerts && alerts.lowFuelWarning);
    if (!hasAlertConfig) {
      return false;
    }
    if (settings && settings.fuelAlertSoundsEnabled !== undefined) {
      return settings.fuelAlertSoundsEnabled !== false;
    }
    return true;
  }

  function alertForModel(actionDefinition, model) {
    if (actionDefinition && actionDefinition.id === "fuel") {
      return fuelAlertForModel(actionDefinition, model);
    }
    if (actionDefinition && actionDefinition.id === "altitude") {
      return groundCollisionAlertForModel(actionDefinition, model);
    }
    return gAlertForModel(actionDefinition, model);
  }

  function gAlertForModel(actionDefinition, model) {
    const alerts = actionDefinition && actionDefinition.alerts
      ? actionDefinition.alerts
      : null;
    if (!alerts || !model) {
      return null;
    }

    if (model.statusKey === "warning") {
      return configuredAlert(
        "warning",
        "gWarning",
        alerts.gWarning,
        alertCooldownMs(alerts.gWarning, 3000)
      );
    }
    if (model.statusKey === "danger") {
      return configuredAlert(
        "danger",
        "gDanger",
        alerts.gDanger || alerts.dangerSound,
        dangerCooldownMs(actionDefinition, model, alerts.gDanger || alerts.dangerSound)
      );
    }
    return null;
  }

  function groundCollisionAlertForModel(actionDefinition, model) {
    const alerts = actionDefinition && actionDefinition.alerts
      ? actionDefinition.alerts
      : null;
    if (!alerts || !model) {
      return null;
    }

    if (model.statusKey === "warning") {
      return configuredAlert(
        "warning",
        "terrainWarning",
        alerts.groundCollisionWarning,
        alertCooldownMs(alerts.groundCollisionWarning, 2200)
      );
    }
    if (model.statusKey === "danger") {
      return configuredAlert(
        "danger",
        "terrainDanger",
        alerts.groundCollisionDanger,
        groundCollisionCooldownMs(model, alerts.groundCollisionDanger, 1400)
      );
    }
    if (model.statusKey === "pullUp") {
      return configuredAlert(
        "pullUp",
        "pullUp",
        alerts.groundCollisionPullUp || alerts.groundCollisionDanger,
        groundCollisionCooldownMs(model, alerts.groundCollisionPullUp || alerts.groundCollisionDanger, 900)
      );
    }
    return null;
  }

  function fuelAlertForModel(actionDefinition, model) {
    const alerts = actionDefinition && actionDefinition.alerts
      ? actionDefinition.alerts
      : null;
    if (
      !alerts ||
      !model ||
      !model.lowFuelWarningActive ||
      model.alertAcknowledged === true
    ) {
      return null;
    }

    const alert = configuredAlert(
      "lowFuel:" + fuelWarningKey(model),
      "lowFuelVoice",
      alerts.lowFuelWarning,
      alertCooldownMs(alerts.lowFuelWarning, 10000)
    );
    if (alert) {
      alert.options = Object.assign({}, alert.options, {
        text: alert.options.text || "LOW FUEL"
      });
    }
    return alert;
  }

  function configuredAlert(stateKey, fallbackPattern, options, fallbackCooldownMs) {
    if (!options || options.enabled === false) {
      return null;
    }
    return {
      stateKey: stateKey,
      pattern: options.pattern || fallbackPattern,
      options: options,
      cooldownMs: Number.isFinite(fallbackCooldownMs)
        ? fallbackCooldownMs
        : numberOrDefault(options.cooldownMs, 2000)
    };
  }

  function dangerCooldownMs(actionDefinition, model, options) {
    const baseCooldownMs = alertCooldownMs(options, 2000);
    const minCooldownMs = Number.isFinite(options && options.minCooldownMs)
      ? options.minCooldownMs
      : 1000;
    const cooldownDropPerG = Number.isFinite(options && options.cooldownDropPerG)
      ? options.cooldownDropPerG
      : 500;
    const thresholds = actionDefinition.thresholds || {};
    const dangerMin = Number.isFinite(thresholds.dangerMin) ? thresholds.dangerMin : 10;
    const value = Number.isFinite(model.value) ? model.value : dangerMin;
    const overDanger = Math.max(0, value - dangerMin);
    return clamp(baseCooldownMs - overDanger * cooldownDropPerG, minCooldownMs, baseCooldownMs);
  }

  function groundCollisionCooldownMs(model, options, fallback) {
    const baseCooldownMs = alertCooldownMs(options, fallback);
    const minCooldownMs = Number.isFinite(options && options.minCooldownMs)
      ? options.minCooldownMs
      : 600;
    const cooldownDropPerRisk = Number.isFinite(options && options.cooldownDropPerRisk)
      ? options.cooldownDropPerRisk
      : 450;
    const riskRatio = Number.isFinite(model && model.riskRatio) ? model.riskRatio : 1;
    const overDanger = Math.max(0, riskRatio - 1);
    return clamp(baseCooldownMs - overDanger * cooldownDropPerRisk, minCooldownMs, baseCooldownMs);
  }

  function alertCooldownMs(options, fallback) {
    return Number.isFinite(options && options.cooldownMs) ? options.cooldownMs : fallback;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function numberOrNull(value) {
    if (value === null || value === undefined || value === "") {
      return null;
    }
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
  }

  function numberOrDefault(value, fallback) {
    const number = numberOrNull(value);
    return number === null ? fallback : number;
  }

  function thresholdNumber(thresholds, key, fallback) {
    return thresholds && Number.isFinite(thresholds[key]) ? thresholds[key] : fallback;
  }

  function maxNumber(values) {
    let maximum = null;
    for (let index = 0; index < values.length; index += 1) {
      const value = numberOrNull(values[index]);
      if (value !== null && (maximum === null || value > maximum)) {
        maximum = value;
      }
    }
    return maximum;
  }

  window.WTDeckActionRuntime = ActionRuntime;
})();
