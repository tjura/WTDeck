(function () {
  class WarThunderClient {
    constructor(options) {
      this.baseUrl = trimSlash(options.baseUrl || "http://127.0.0.1:8111");
      this.requestTimeoutMs = options.requestTimeoutMs || 120;
      this.lastGoodAt = 0;
      this.lastError = null;
    }

    async readSnapshot() {
      const now = Date.now();
      const [stateResult, indicatorsResult] = await Promise.allSettled([
        this.fetchJson("/state"),
        this.fetchJson("/indicators")
      ]);

      const state = settledValue(stateResult);
      const indicators = settledValue(indicatorsResult);
      const connected = Boolean(state || indicators);

      if (connected) {
        this.lastGoodAt = now;
        this.lastError = null;
      } else {
        this.lastError = stateResult.reason || indicatorsResult.reason || new Error("No telemetry");
      }

      return normalizeSnapshot({
        connected: connected,
        readAt: now,
        lastGoodAt: this.lastGoodAt,
        state: state || {},
        indicators: indicators || {},
        error: this.lastError
      });
    }

    async fetchJson(path) {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.requestTimeoutMs);
      try {
        const response = await fetch(this.baseUrl + path, {
          cache: "no-store",
          signal: controller.signal
        });
        if (!response.ok) {
          throw new Error(path + " returned HTTP " + response.status);
        }
        return await response.json();
      } finally {
        clearTimeout(timeoutId);
      }
    }
  }

  function normalizeSnapshot(raw) {
    const state = raw.state || {};
    const indicators = raw.indicators || {};
    const gearPercent = firstPercent([
      state["gear, %"],
      indicators.gears_indicator,
      indicators.gears,
      indicators.gears_lamp
    ]);

    return {
      connected: raw.connected,
      valid: isFlightTelemetryValid(state, indicators, raw.connected),
      readAt: raw.readAt,
      lastGoodAt: raw.lastGoodAt,
      state: state,
      indicators: indicators,
      errorMessage: raw.error ? raw.error.message : "",
      aircraftType: indicators.type || "",
      army: indicators.army || "",
      gearPercent: gearPercent
    };
  }

  function firstPercent(values) {
    for (let index = 0; index < values.length; index += 1) {
      const normalized = normalizePercent(values[index]);
      if (normalized !== null) {
        return normalized;
      }
    }
    return null;
  }

  function normalizePercent(value) {
    if (typeof value === "boolean") {
      return value ? 100 : 0;
    }
    if (typeof value === "string") {
      const normalized = value.trim().toLowerCase();
      if (["down", "deployed", "extended", "on", "true"].includes(normalized)) {
        return 100;
      }
      if (["up", "retracted", "off", "false"].includes(normalized)) {
        return 0;
      }
    }
    const number = numberOrNull(value);
    if (number === null) {
      return null;
    }
    if (number >= 0 && number <= 1) {
      return clamp(number * 100, 0, 100);
    }
    return clamp(number, 0, 100);
  }

  function numberOrNull(value) {
    if (value === null || value === undefined || value === "") {
      return null;
    }
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
  }

  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }

  function settledValue(result) {
    return result.status === "fulfilled" ? result.value : null;
  }

  function isFlightTelemetryValid(state, indicators, connected) {
    if (!connected || state.valid === false || indicators.valid === false) {
      return false;
    }
    if (state.valid === true || indicators.valid === true) {
      return true;
    }
    return [
      state["gear, %"],
      state["IAS, km/h"],
      state["H, m"],
      indicators.gears_indicator,
      indicators.type,
      indicators.army
    ].some((value) => value !== null && value !== undefined && value !== "");
  }

  function trimSlash(value) {
    return String(value).replace(/\/+$/, "");
  }

  window.WTDeckWarThunderClient = WarThunderClient;
})();
