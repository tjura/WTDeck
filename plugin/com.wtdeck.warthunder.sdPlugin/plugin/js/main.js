(function () {
  async function boot(port, pluginUuid, registerEvent, info, actionInfo) {
    const bridge = new window.WTDeckStreamDockBridge(
      port,
      pluginUuid,
      registerEvent,
      info,
      actionInfo
    );
    const defaults = await loadJson("../config/defaults.json", window.WTDECK_DEFAULTS);
    const actionConfig = await loadJson("../config/actions.json", window.WTDECK_ACTION_CONFIG);
    const runtime = new window.WTDeckActionRuntime(bridge, actionConfig, defaults);
    bridge.connect();
    runtime.start();
  }

  async function loadJson(path, fallback) {
    try {
      const response = await fetch(path, { cache: "no-store" });
      if (!response.ok) {
        throw new Error(path + " returned HTTP " + response.status);
      }
      return await response.json();
    } catch (_error) {
      return fallback;
    }
  }

  window.connectElgatoStreamDeckSocket = function (
    port,
    pluginUuid,
    registerEvent,
    info,
    actionInfo
  ) {
    boot(port, pluginUuid, registerEvent, info, actionInfo);
  };
})();
