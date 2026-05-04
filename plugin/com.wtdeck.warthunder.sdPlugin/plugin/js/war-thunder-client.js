(function () {
  class WarThunderClient {
    constructor(options) {
      this.baseUrl = trimSlash(options.baseUrl || "http://127.0.0.1:8111");
      this.requestTimeoutMs = options.requestTimeoutMs || 120;
      this.lastGoodAt = 0;
      this.lastError = null;
      this.lastRadarAltitudeSample = null;
      this.radarClosureRateMps = null;
      this.fuelSamples = [];
      this.fuelBurnKgPerSec = null;
      this.lastFuelKg = null;
      this.lastFuelAircraftKey = "";
      this.fuelTrackingActive = false;
      this.fuelSessionId = 0;
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

      const snapshot = normalizeSnapshot({
        connected: connected,
        readAt: now,
        lastGoodAt: this.lastGoodAt,
        state: state || {},
        indicators: indicators || {},
        error: this.lastError
      });
      return this.enrichDerivedTelemetry(snapshot);
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

    enrichDerivedTelemetry(snapshot) {
      this.enrichFuelTelemetry(snapshot);

      const radarAltitudeMeters = numberOrNull(snapshot.radarAltitudeMeters);
      const readAt = Number.isFinite(snapshot.readAt) ? snapshot.readAt : Date.now();

      if (!snapshot.connected || !snapshot.valid || radarAltitudeMeters === null) {
        this.lastRadarAltitudeSample = null;
        this.radarClosureRateMps = null;
        snapshot.derivedSinkRateMps = null;
        snapshot.verticalSinkRateMps = null;
        snapshot.groundClosureRateMps = null;
        return snapshot;
      }

      const verticalSinkRateMps = Number.isFinite(snapshot.verticalSpeedMps)
        ? Math.max(0, -snapshot.verticalSpeedMps)
        : null;
      let derivedSinkRateMps = null;

      if (this.lastRadarAltitudeSample) {
        const dt = (readAt - this.lastRadarAltitudeSample.readAt) / 1000;
        if (dt >= 0.05 && dt <= 2) {
          derivedSinkRateMps = Math.max(
            0,
            (this.lastRadarAltitudeSample.radarAltitudeMeters - radarAltitudeMeters) / dt
          );
        } else {
          this.radarClosureRateMps = null;
        }
      }

      const closureCandidates = [verticalSinkRateMps, derivedSinkRateMps].filter((value) =>
        Number.isFinite(value)
      );
      const rawClosureRateMps = closureCandidates.length
        ? Math.max.apply(null, closureCandidates)
        : null;

      if (rawClosureRateMps === null) {
        this.radarClosureRateMps = null;
      } else if (this.radarClosureRateMps === null) {
        this.radarClosureRateMps = rawClosureRateMps;
      } else {
        this.radarClosureRateMps = this.radarClosureRateMps * 0.65 + rawClosureRateMps * 0.35;
      }

      this.lastRadarAltitudeSample = {
        readAt: readAt,
        radarAltitudeMeters: radarAltitudeMeters
      };
      snapshot.derivedSinkRateMps = derivedSinkRateMps;
      snapshot.verticalSinkRateMps = verticalSinkRateMps;
      snapshot.groundClosureRateMps = this.radarClosureRateMps;
      return snapshot;
    }

    enrichFuelTelemetry(snapshot) {
      const readAt = Number.isFinite(snapshot.readAt) ? snapshot.readAt : Date.now();
      const fuelKg = numberOrNull(snapshot.fuelKg);
      const initialFuelKg = numberOrNull(snapshot.initialFuelKg);
      snapshot.fuelBurnKgPerSec = null;
      snapshot.fuelBurnTrusted = false;
      snapshot.fuelEtaSeconds = null;
      snapshot.fuelSampleWindowMs = 0;
      snapshot.fuelDropWindowKg = null;
      snapshot.fuelSessionId = this.fuelSessionId;

      if (!snapshot.connected || !snapshot.valid || fuelKg === null) {
        if (this.fuelTrackingActive || this.fuelSamples.length > 0) {
          this.resetFuelEstimate(true);
        }
        this.fuelTrackingActive = false;
        snapshot.fuelSessionId = this.fuelSessionId;
        return snapshot;
      }

      const aircraftKey = [
        snapshot.aircraftType || "",
        snapshot.army || "",
        initialFuelKg === null ? "" : String(Math.round(initialFuelKg))
      ].join("|");
      if (!this.fuelTrackingActive || aircraftKey !== this.lastFuelAircraftKey) {
        this.resetFuelEstimate(true);
        this.lastFuelAircraftKey = aircraftKey;
        this.fuelTrackingActive = true;
      }

      const refuelIncreaseKg = Math.max(3, initialFuelKg === null ? 3 : initialFuelKg * 0.005);
      if (this.lastFuelKg !== null && fuelKg > this.lastFuelKg + refuelIncreaseKg) {
        this.resetFuelEstimate(true);
        this.lastFuelAircraftKey = aircraftKey;
        this.fuelTrackingActive = true;
      }
      this.lastFuelKg = fuelKg;
      snapshot.fuelSessionId = this.fuelSessionId;

      this.addFuelSample(readAt, fuelKg);
      const estimate = this.updateFuelBurnEstimate(readAt, fuelKg);
      snapshot.fuelBurnKgPerSec = estimate.burnKgPerSec;
      snapshot.fuelBurnTrusted = estimate.trusted;
      snapshot.fuelEtaSeconds = estimate.trusted && estimate.burnKgPerSec > 0
        ? fuelKg / estimate.burnKgPerSec
        : null;
      snapshot.fuelSampleWindowMs = estimate.windowMs;
      snapshot.fuelDropWindowKg = estimate.dropKg;
      return snapshot;
    }

    addFuelSample(readAt, fuelKg) {
      const lastSample = this.fuelSamples.length
        ? this.fuelSamples[this.fuelSamples.length - 1]
        : null;
      if (!lastSample || lastSample.readAt !== readAt) {
        this.fuelSamples.push({ readAt: readAt, fuelKg: fuelKg });
      }

      const maxWindowMs = 22000;
      while (
        this.fuelSamples.length > 1 &&
        readAt - this.fuelSamples[0].readAt > maxWindowMs
      ) {
        this.fuelSamples.shift();
      }
    }

    updateFuelBurnEstimate(readAt, fuelKg) {
      const minimumWindowMs = 4000;
      const minimumDropKg = 0.05;
      const minimumTrustedBurnKgPerSec = 0.005;
      const oldest = this.oldestFuelSample(readAt, minimumWindowMs);
      if (!oldest) {
        return this.currentFuelEstimate(0, null, false);
      }

      const windowMs = Math.max(0, readAt - oldest.readAt);
      const dropKg = Math.max(0, oldest.fuelKg - fuelKg);
      if (windowMs < minimumWindowMs) {
        return this.currentFuelEstimate(windowMs, dropKg, false);
      }

      if (dropKg < minimumDropKg) {
        if (this.fuelBurnKgPerSec !== null) {
          this.fuelBurnKgPerSec *= 0.85;
          if (this.fuelBurnKgPerSec < minimumTrustedBurnKgPerSec) {
            this.fuelBurnKgPerSec = null;
          }
        }
        return this.currentFuelEstimate(windowMs, dropKg, false);
      }

      const rawBurnKgPerSec = dropKg / (windowMs / 1000);
      if (!Number.isFinite(rawBurnKgPerSec) || rawBurnKgPerSec <= 0) {
        return this.currentFuelEstimate(windowMs, dropKg, false);
      }

      if (this.fuelBurnKgPerSec === null) {
        this.fuelBurnKgPerSec = rawBurnKgPerSec;
      } else {
        const weight = rawBurnKgPerSec > this.fuelBurnKgPerSec ? 0.55 : 0.3;
        this.fuelBurnKgPerSec =
          this.fuelBurnKgPerSec * (1 - weight) + rawBurnKgPerSec * weight;
      }

      return this.currentFuelEstimate(
        windowMs,
        dropKg,
        this.fuelBurnKgPerSec >= minimumTrustedBurnKgPerSec
      );
    }

    oldestFuelSample(readAt, minimumWindowMs) {
      let sample = null;
      for (let index = 0; index < this.fuelSamples.length; index += 1) {
        if (readAt - this.fuelSamples[index].readAt >= minimumWindowMs) {
          sample = this.fuelSamples[index];
        } else {
          break;
        }
      }
      return sample;
    }

    currentFuelEstimate(windowMs, dropKg, trusted) {
      return {
        burnKgPerSec: this.fuelBurnKgPerSec,
        trusted: Boolean(trusted && this.fuelBurnKgPerSec !== null),
        windowMs: windowMs,
        dropKg: dropKg
      };
    }

    resetFuelEstimate(incrementSession) {
      this.fuelSamples = [];
      this.fuelBurnKgPerSec = null;
      this.lastFuelKg = null;
      if (incrementSession) {
        this.fuelSessionId += 1;
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
    const airbrakePercent = firstPercent([
      state["airbrake, %"],
      indicators.airbrake_indicator,
      indicators.airbrake_lever
    ]);
    const flapsPercent = firstPercent([
      state["flaps, %"],
      indicators.flaps_indicator,
      indicators.flaps
    ]);
    const gForce = firstNumber([
      state.Ny,
      indicators.g_meter
    ]);
    const iasKmh = firstNumber([
      state["IAS, km/h"]
    ]);
    const tasKmh = firstNumber([
      state["TAS, km/h"]
    ]);
    const throttlePercent = firstPercent([
      state["throttle 1, %"],
      indicators.throttle
    ]);
    const fuelKg = firstNumber([
      indicators.fuel,
      state["Mfuel, kg"],
      sumMatchingNumberFields(state, /^Mfuel \d+, kg$/)
    ]);
    const initialFuelKg = firstNumber([
      state["Mfuel0, kg"],
      sumMatchingNumberFields(state, /^Mfuel0 \d+, kg$/)
    ]);
    const fuelConsume = firstNumber([
      indicators.fuel_consume
    ]);
    const fuelPercent = fuelKg !== null && initialFuelKg !== null && initialFuelKg > 0
      ? clamp((fuelKg / initialFuelKg) * 100, 0, 100)
      : null;
    const altitudeMeters = firstNumber([
      state["H, m"]
    ]);
    const rawRadarAltitude = firstNumber([
      indicators.radio_altitude
    ]);
    const radarAltitudeMeters = normalizeRadarAltitudeMeters(rawRadarAltitude);
    const verticalSpeedMps = firstNumber([
      state["Vy, m/s"],
      indicators.vario
    ]);
    const pitchDeg = firstNumber([
      indicators.aviahorizon_pitch,
      indicators.aviahorizon_pitch1
    ]);
    const rollDeg = firstNumber([
      indicators.aviahorizon_roll,
      indicators.aviahorizon_roll1,
      indicators.bank
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
      gearPercent: gearPercent,
      airbrakePercent: airbrakePercent,
      flapsPercent: flapsPercent,
      gForce: gForce,
      iasKmh: iasKmh,
      tasKmh: tasKmh,
      throttlePercent: throttlePercent,
      fuelKg: fuelKg,
      initialFuelKg: initialFuelKg,
      fuelPercent: fuelPercent,
      fuelConsume: fuelConsume,
      fuelBurnKgPerSec: null,
      fuelBurnTrusted: false,
      fuelEtaSeconds: null,
      fuelSampleWindowMs: 0,
      fuelDropWindowKg: null,
      fuelSessionId: 0,
      altitudeMeters: altitudeMeters,
      radarAltitudeMeters: radarAltitudeMeters,
      verticalSpeedMps: verticalSpeedMps,
      pitchDeg: pitchDeg,
      rollDeg: rollDeg,
      derivedSinkRateMps: null,
      verticalSinkRateMps: null,
      groundClosureRateMps: null
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

  function firstNumber(values) {
    for (let index = 0; index < values.length; index += 1) {
      const number = numberOrNull(values[index]);
      if (number !== null) {
        return number;
      }
    }
    return null;
  }

  function sumMatchingNumberFields(source, pattern) {
    let sum = 0;
    let found = false;
    Object.keys(source || {}).forEach((name) => {
      if (!pattern.test(name)) {
        return;
      }
      const number = numberOrNull(source[name]);
      if (number !== null) {
        sum += number;
        found = true;
      }
    });
    return found ? sum : null;
  }

  function normalizeRadarAltitudeMeters(value) {
    const number = numberOrNull(value);
    if (number === null) {
      return null;
    }
    return Math.max(0, number * 0.3048);
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
      state["airbrake, %"],
      state["flaps, %"],
      state["IAS, km/h"],
      state["TAS, km/h"],
      state["H, m"],
      state["Vy, m/s"],
      state["Mfuel, kg"],
      state["Mfuel0, kg"],
      state.Ny,
      indicators.fuel,
      indicators.fuel_consume,
      indicators.vario,
      indicators.radio_altitude,
      indicators.aviahorizon_pitch,
      indicators.aviahorizon_pitch1,
      indicators.aviahorizon_roll,
      indicators.aviahorizon_roll1,
      indicators.bank,
      indicators.gears_indicator,
      indicators.airbrake_indicator,
      indicators.airbrake_lever,
      indicators.flaps_indicator,
      indicators.flaps,
      indicators.g_meter,
      indicators.type,
      indicators.army
    ].some((value) => value !== null && value !== undefined && value !== "");
  }

  function trimSlash(value) {
    return String(value).replace(/\/+$/, "");
  }

  window.WTDeckWarThunderClient = WarThunderClient;
})();
