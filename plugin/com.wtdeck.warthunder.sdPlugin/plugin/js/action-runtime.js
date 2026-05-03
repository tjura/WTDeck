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
    }

    start() {
      this.bridge.on("willAppear", (event) => this.onWillAppear(event));
      this.bridge.on("willDisappear", (event) => this.onWillDisappear(event));
      this.bridge.on("didReceiveSettings", (event) => this.onDidReceiveSettings(event));
      this.bridge.on("keyDown", (event) => this.onKeyDown(event));
      this.bridge.on("keyUp", (event) => this.onKeyUp(event));
      this.bridge.on("connected", () => this.bridge.logMessage("Plugin runtime connected."));
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
        actionUuid: event.action,
        settings: event.payload && event.payload.settings ? event.payload.settings : {},
        renderKey: ""
      });
      this.bridge.getSettings(event.context);
      this.renderContext(event.context);
    }

    onWillDisappear(event) {
      this.contexts.delete(event.context);
    }

    onDidReceiveSettings(event) {
      const context = this.contexts.get(event.context);
      if (!context) {
        return;
      }
      context.settings = event.payload && event.payload.settings ? event.payload.settings : {};
      context.renderKey = "";
      this.renderContext(event.context);
    }

    async onKeyDown(event) {
      const context = this.contexts.get(event.context);
      if (!context) {
        return;
      }
      const actionDefinition = this.actionConfig.actions[context.actionUuid];
      const handled = await this.dispatchCommand(actionDefinition, context.settings, "down");
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
      const handled = await this.dispatchCommand(actionDefinition, context.settings, "up");
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
      this.renderAll();
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
      const model = window.WTDeckStateMachines.modelForAction(
        actionDefinition,
        this.telemetry,
        context.settings
      );
      const renderKey = JSON.stringify({ action: context.actionUuid, model: model });
      if (renderKey === context.renderKey) {
        return;
      }
      context.renderKey = renderKey;
      this.bridge.setTitle(contextId, "");
      this.bridge.setImage(contextId, window.WTDeckKeyRenderer.render(actionDefinition, model));
    }

    async dispatchCommand(actionDefinition, settings, phase) {
      const command = actionDefinition.command || {};
      const adapter = normalizeAdapter(
        settings.commandAdapter || command.adapter || this.defaults.commands.defaultAdapter
      );
      if (adapter === "none" || command.intent === "none") {
        return true;
      }
      if (adapter === "native-streamdock-hotkey") {
        this.bridge.logMessage(
          "Native Stream Dock hotkey is unsupported for custom code actions; falling back to companion for '" +
            command.intent +
            "' (" +
            (command.defaultHotkeyLabel || "configured manifest key") +
            ")."
        );
        return this.dispatchCompanionCommand(actionDefinition, settings, phase);
      }
      if (adapter === "companion-http") {
        return this.dispatchCompanionCommand(actionDefinition, settings, phase);
      }

      this.bridge.logMessage(
        "Command '" + command.intent + "' is not dispatched because adapter is '" + adapter + "'."
      );
      return false;
    }

    async dispatchCompanionCommand(actionDefinition, settings, phase) {
      const url = settings.companionUrl || this.defaults.commands.companionUrl;
      const controller = new AbortController();
      const timeoutId = window.setTimeout(
        () => controller.abort(),
        this.defaults.commands.companionTimeoutMs
      );
      try {
        const response = await fetch(url, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            intent: actionDefinition.command.intent,
            hotkeyLabel: settings.hotkeyLabel || actionDefinition.command.defaultHotkeyLabel || "",
            phase: phase || "tap",
            source: "streamdock",
            plugin: "com.wtdeck.warthunder"
          }),
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
  }

  function normalizeAdapter(adapter) {
    if (adapter === "unassigned") {
      return "none";
    }
    return adapter || "none";
  }

  window.WTDeckActionRuntime = ActionRuntime;
})();
