(function () {
  class StreamDockBridge {
    constructor(port, pluginUuid, registerEvent, info, actionInfo) {
      this.port = port;
      this.pluginUuid = pluginUuid;
      this.registerEvent = registerEvent;
      this.info = safeJson(info);
      this.actionInfo = safeJson(actionInfo);
      this.socket = null;
      this.handlers = new Map();
    }

    connect() {
      this.socket = new WebSocket("ws://127.0.0.1:" + this.port);

      this.socket.onopen = () => {
        this.send({
          event: this.registerEvent,
          uuid: this.pluginUuid
        });
        this.emit("connected", {});
      };

      this.socket.onmessage = (message) => {
        const event = safeJson(message.data);
        if (event && event.event) {
          this.emit(event.event, event);
        }
      };

      this.socket.onclose = () => this.emit("disconnected", {});
      this.socket.onerror = () => this.emit("error", {});
    }

    on(eventName, handler) {
      if (!this.handlers.has(eventName)) {
        this.handlers.set(eventName, []);
      }
      this.handlers.get(eventName).push(handler);
    }

    emit(eventName, payload) {
      const handlers = this.handlers.get(eventName) || [];
      handlers.forEach((handler) => handler(payload));
    }

    send(payload) {
      if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
        return;
      }
      this.socket.send(JSON.stringify(payload));
    }

    setTitle(context, title) {
      this.send({
        event: "setTitle",
        context: context,
        payload: {
          target: 0,
          title: title || ""
        }
      });
    }

    setImage(context, imageDataUrl) {
      this.send({
        event: "setImage",
        context: context,
        payload: {
          target: 0,
          image: imageDataUrl
        }
      });
    }

    showOk(context) {
      this.send({ event: "showOk", context: context });
    }

    showAlert(context) {
      this.send({ event: "showAlert", context: context });
    }

    getSettings(context) {
      this.send({ event: "getSettings", context: context });
    }

    setSettings(context, settings) {
      this.send({
        event: "setSettings",
        context: context,
        payload: settings || {}
      });
    }

    logMessage(message) {
      this.send({
        event: "logMessage",
        payload: {
          message: "[WTDeck] " + message
        }
      });
    }
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

  window.WTDeckStreamDockBridge = StreamDockBridge;
})();
