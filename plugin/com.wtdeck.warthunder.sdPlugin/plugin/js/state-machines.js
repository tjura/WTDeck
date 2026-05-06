(function () {
  const groundCollisionMemoryByAction = new Map();
  const fuelMemoryByAction = new Map();
  const COLLISION_SEVERITY = {
    safe: 0,
    warning: 1,
    danger: 2,
    pullUp: 3
  };

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
    const valid = Boolean(telemetry && telemetry.activeFlight);
    const inverted = Boolean(settings && settings.invertTelemetry);

    if (actionDefinition.id === "speed") {
      return modelForSpeedReadout(actionDefinition, telemetry, connected, valid);
    }

    if (actionDefinition.id === "fuel") {
      return modelForFuelReadout(actionDefinition, telemetry, connected, valid);
    }

    if (actionDefinition.id === "altitude") {
      return modelForAltitudeReadout(actionDefinition, telemetry, connected, valid);
    }

    if (actionDefinition.id === "drogueChute") {
      return modelForDrogueChute(actionDefinition, telemetry, connected, valid);
    }

    if (isIndicatorAction(actionDefinition)) {
      return modelForIndicator(actionDefinition, telemetry, connected, valid);
    }

    if (!connected || !valid) {
      return {
        actionId: actionId,
        connected: connected,
        statusKey: "unknown",
        statusText: connected ? "NO FLIGHT" : "OFFLINE",
        percent: null,
        tone: "offline"
      };
    }

    if (usesFlightValidityModel(actionDefinition)) {
      return {
        actionId: actionId,
        connected: connected,
        statusKey: "on",
        statusText: actionDefinition.states.on || "READY",
        percent: null,
        tone: "safe"
      };
    }

    const fieldName = actionDefinition.telemetry && actionDefinition.telemetry.normalizedField;
    if (!fieldName) {
      return {
        actionId: actionId,
        connected: connected,
        statusKey: "unknown",
        statusText: actionDefinition.states.unknown || "UNKNOWN",
        percent: null,
        tone: "offline"
      };
    }
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
      percent: percent,
      tone: toneForState(stateKey)
    };
  }

  function modelForIndicator(actionDefinition, telemetry, connected, valid) {
    const actionId = actionDefinition.id;
    if (!connected || !valid) {
      return indicatorFallbackModel(actionDefinition, connected);
    }

    const fieldName = actionDefinition.telemetry && actionDefinition.telemetry.normalizedField;
    const value = fieldName ? telemetry[fieldName] : null;
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return indicatorFallbackModel(actionDefinition, connected);
    }

    const stateKey = classifyGForce(value, actionDefinition.thresholds);
    const statusText = indicatorStateTextFor(actionDefinition, stateKey);

    return {
      actionId: actionId,
      connected: connected,
      statusKey: stateKey,
      statusText: statusText,
      percent: null,
      value: value,
      valueText: formatSignedG(value),
      tone: toneForIndicatorState(stateKey),
      blinkOn: stateKey === "danger" ? blinkOn(telemetry.readAt) : true
    };
  }

  function indicatorFallbackModel(actionDefinition, connected) {
    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "unknown",
      statusText: connected ? actionDefinition.states.unknown || "NO FLIGHT" : "OFFLINE",
      percent: null,
      value: null,
      valueText: "--.-G",
      tone: "offline",
      blinkOn: false
    };
  }

  function modelForSpeedReadout(actionDefinition, telemetry, connected, valid) {
    if (!connected || !valid) {
      return speedFallbackModel(actionDefinition, connected);
    }

    const iasValue = numberOrNull(telemetry.iasKmh);
    const tasValue = numberOrNull(telemetry.tasKmh);
    if (iasValue === null && tasValue === null) {
      return speedFallbackModel(actionDefinition, connected);
    }

    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "safe",
      statusText: actionDefinition.states.safe || "LIVE",
      percent: null,
      iasValue: iasValue,
      tasValue: tasValue,
      iasText: formatSpeed(iasValue),
      tasText: formatSpeed(tasValue),
      tone: "safe"
    };
  }

  function speedFallbackModel(actionDefinition, connected) {
    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "unknown",
      statusText: connected ? actionDefinition.states.unknown || "NO FLIGHT" : "OFFLINE",
      percent: null,
      iasValue: null,
      tasValue: null,
      iasText: "---",
      tasText: "---",
      tone: "offline"
    };
  }

  function modelForFuelReadout(actionDefinition, telemetry, connected, valid) {
    if (!connected || !valid) {
      return fuelFallbackModel(actionDefinition, connected);
    }

    const fuelKg = numberOrNull(telemetry.fuelKg);
    if (fuelKg === null) {
      return fuelFallbackModel(actionDefinition, connected);
    }

    const fuel = classifyFuelEndurance(actionDefinition, telemetry, fuelKg);
    const stateKey = fuel.stateKey;

    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: stateKey,
      statusText: indicatorStateTextFor(actionDefinition, stateKey),
      percent: null,
      fuelKg: fuelKg,
      initialFuelKg: numberOrNull(telemetry.initialFuelKg),
      fuelPercent: numberOrNull(telemetry.fuelPercent),
      fuelBurnKgPerSec: numberOrNull(telemetry.fuelBurnKgPerSec),
      fuelBurnTrusted: Boolean(telemetry.fuelBurnTrusted),
      fuelEtaSeconds: fuel.fuelEtaSeconds,
      fuelSessionId: Number.isFinite(telemetry.fuelSessionId) ? telemetry.fuelSessionId : 0,
      lowFuelWarningKey: fuel.warningKey,
      lowFuelWarningActive: stateKey === "warning",
      fuelMassText: formatFuelMass(fuelKg),
      fuelPercentText: formatFuelPercent(telemetry.fuelPercent),
      fuelEtaText: formatFuelEta(fuel.fuelEtaSeconds),
      fuelBurnText: formatFuelBurn(telemetry.fuelBurnKgPerSec),
      tone: toneForFuelState(stateKey),
      blinkOn: stateKey === "warning" ? blinkOn(telemetry.readAt) : true
    };
  }

  function fuelFallbackModel(actionDefinition, connected) {
    fuelMemoryByAction.delete(actionDefinition.id);
    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "unknown",
      statusText: connected ? actionDefinition.states.unknown || "NO FLIGHT" : "OFFLINE",
      percent: null,
      fuelKg: null,
      initialFuelKg: null,
      fuelPercent: null,
      fuelBurnKgPerSec: null,
      fuelBurnTrusted: false,
      fuelEtaSeconds: null,
      fuelSessionId: 0,
      lowFuelWarningKey: "",
      lowFuelWarningActive: false,
      fuelMassText: "----",
      fuelPercentText: "--%",
      fuelEtaText: "--:--",
      fuelBurnText: "--KG/M",
      tone: "offline",
      blinkOn: false
    };
  }

  function modelForAltitudeReadout(actionDefinition, telemetry, connected, valid) {
    if (!connected || !valid) {
      return altitudeFallbackModel(actionDefinition, connected);
    }

    const altitudeMeters = numberOrNull(telemetry.altitudeMeters);
    const radarAltitudeMeters = numberOrNull(telemetry.radarAltitudeMeters);
    if (altitudeMeters === null && radarAltitudeMeters === null) {
      return altitudeFallbackModel(actionDefinition, connected);
    }

    const collision = classifyGroundCollision(actionDefinition, telemetry);
    const stateKey = collision.stateKey;
    const statusText = indicatorStateTextFor(actionDefinition, stateKey);
    const alerting = isGroundCollisionAlertState(stateKey);

    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: stateKey,
      statusText: statusText,
      percent: null,
      altitudeMeters: altitudeMeters,
      radarAltitudeMeters: radarAltitudeMeters,
      altitudeText: formatMeters(altitudeMeters),
      radarAltitudeText: formatMeters(radarAltitudeMeters),
      verticalSpeedMps: collision.verticalSpeedMps,
      groundClosureRateMps: collision.groundClosureRateMps,
      derivedSinkRateMps: collision.derivedSinkRateMps,
      speedMps: collision.speedMps,
      riskRatio: collision.riskRatio,
      requiredAltitudeMeters: collision.requiredAltitudeMeters,
      timeToImpactSec: collision.timeToImpactSec,
      descentAngleDeg: collision.descentAngleDeg,
      closureRateText: formatClosureRate(collision.groundClosureRateMps),
      timeToImpactText: formatTimeToImpact(collision.timeToImpactSec),
      descentAngleText: formatAngle(collision.descentAngleDeg),
      suppressedReason: collision.suppressedReason,
      tone: toneForIndicatorState(stateKey),
      blinkOn: alerting ? blinkOn(telemetry.readAt) : true
    };
  }

  function altitudeFallbackModel(actionDefinition, connected) {
    groundCollisionMemoryByAction.delete(actionDefinition.id);
    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "unknown",
      statusText: connected ? actionDefinition.states.unknown || "NO FLIGHT" : "OFFLINE",
      percent: null,
      altitudeMeters: null,
      radarAltitudeMeters: null,
      altitudeText: "----",
      radarAltitudeText: "----",
      verticalSpeedMps: null,
      groundClosureRateMps: null,
      derivedSinkRateMps: null,
      speedMps: null,
      riskRatio: null,
      requiredAltitudeMeters: null,
      timeToImpactSec: null,
      descentAngleDeg: null,
      closureRateText: "--",
      timeToImpactText: "--",
      descentAngleText: "--",
      suppressedReason: "",
      tone: "offline",
      blinkOn: false
    };
  }

  function modelForDrogueChute(actionDefinition, telemetry, connected, valid) {
    if (!connected || !valid) {
      return drogueFallbackModel(actionDefinition, connected, valid);
    }

    const iasKmh = numberOrNull(telemetry.iasKmh);
    const radarAltitudeMeters = numberOrNull(telemetry.radarAltitudeMeters);
    if (iasKmh === null || radarAltitudeMeters === null) {
      return drogueFallbackModel(actionDefinition, connected, valid);
    }

    const stateKey = classifyDrogueReadiness(
      iasKmh,
      radarAltitudeMeters,
      actionDefinition.thresholds
    );

    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: stateKey,
      statusText: drogueStateTextFor(actionDefinition, stateKey),
      percent: null,
      iasKmh: iasKmh,
      radarAltitudeMeters: radarAltitudeMeters,
      tone: stateKey === "on" ? "safe" : "offline",
      commandReady: stateKey === "on",
      activeFlight: valid
    };
  }

  function drogueFallbackModel(actionDefinition, connected, valid) {
    return {
      actionId: actionDefinition.id,
      connected: connected,
      statusKey: "unknown",
      statusText: connected
        ? valid ? "NO DATA" : actionDefinition.states.unknown || "NO FLIGHT"
        : "OFFLINE",
      percent: null,
      iasKmh: null,
      radarAltitudeMeters: null,
      tone: "offline",
      commandReady: false,
      activeFlight: Boolean(valid)
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

  function classifyFuelEndurance(actionDefinition, telemetry, fuelKg) {
    const thresholds = actionDefinition.thresholds || {};
    const warningSeconds = thresholdNumber(thresholds, "warningSeconds", 180);
    const clearSeconds = thresholdNumber(thresholds, "clearSeconds", 210);
    const warningPersistenceMs = thresholdNumber(thresholds, "warningPersistenceMs", 1000);
    const clearPersistenceMs = thresholdNumber(thresholds, "clearPersistenceMs", 1000);
    const minimumFuelKg = thresholdNumber(thresholds, "minimumFuelKg", 0.5);
    const now = Number.isFinite(telemetry.readAt) ? telemetry.readAt : Date.now();
    const fuelSessionId = Number.isFinite(telemetry.fuelSessionId) ? telemetry.fuelSessionId : 0;
    const trusted = Boolean(telemetry.fuelBurnTrusted);
    const etaSeconds = trusted && Number.isFinite(telemetry.fuelEtaSeconds)
      ? Math.max(0, telemetry.fuelEtaSeconds)
      : null;
    let memory = fuelMemoryByAction.get(actionDefinition.id);
    if (!memory || memory.fuelSessionId !== fuelSessionId) {
      memory = {
        fuelSessionId: fuelSessionId,
        state: "safe",
        pendingWarningSince: 0,
        pendingClearSince: 0,
        warningStartedAt: 0
      };
      fuelMemoryByAction.set(actionDefinition.id, memory);
    }

    if (fuelKg <= minimumFuelKg) {
      enterFuelWarning(memory, now);
      return fuelClassification(memory, 0);
    }

    if (etaSeconds === null) {
      memory.pendingWarningSince = 0;
      memory.pendingClearSince = 0;
      if (memory.state === "warning") {
        return fuelClassification(memory, null);
      }
      return {
        stateKey: "calibrating",
        fuelEtaSeconds: null,
        warningKey: ""
      };
    }

    if (etaSeconds <= warningSeconds) {
      memory.pendingClearSince = 0;
      if (!memory.pendingWarningSince) {
        memory.pendingWarningSince = now;
      }
      if (memory.state === "warning" || now - memory.pendingWarningSince >= warningPersistenceMs) {
        enterFuelWarning(memory, now);
      }
      return fuelClassification(memory, etaSeconds);
    }

    memory.pendingWarningSince = 0;
    if (memory.state === "warning" && etaSeconds < clearSeconds) {
      memory.pendingClearSince = 0;
      return fuelClassification(memory, etaSeconds);
    }

    if (memory.state === "warning") {
      if (!memory.pendingClearSince) {
        memory.pendingClearSince = now;
      }
      if (now - memory.pendingClearSince < clearPersistenceMs) {
        return fuelClassification(memory, etaSeconds);
      }
    }

    memory.state = "safe";
    memory.pendingClearSince = 0;
    memory.warningStartedAt = 0;
    return {
      stateKey: "safe",
      fuelEtaSeconds: etaSeconds,
      warningKey: ""
    };
  }

  function enterFuelWarning(memory, now) {
    if (memory.state !== "warning") {
      memory.warningStartedAt = now;
    }
    memory.state = "warning";
    memory.pendingWarningSince = 0;
  }

  function fuelClassification(memory, etaSeconds) {
    if (memory.state !== "warning") {
      return {
        stateKey: "safe",
        fuelEtaSeconds: etaSeconds,
        warningKey: ""
      };
    }
    return {
      stateKey: "warning",
      fuelEtaSeconds: etaSeconds,
      warningKey: memory.fuelSessionId + ":" + memory.warningStartedAt
    };
  }

  function classifyGForce(value, thresholds) {
    const warningMin = thresholds && Number.isFinite(thresholds.warningMin)
      ? thresholds.warningMin
      : 9;
    const dangerMin = thresholds && Number.isFinite(thresholds.dangerMin)
      ? thresholds.dangerMin
      : 10;
    if (value > dangerMin) {
      return "danger";
    }
    if (value > warningMin) {
      return "warning";
    }
    return "safe";
  }

  function classifyRadarAltitude(value, thresholds) {
    if (value === null) {
      return "safe";
    }
    const lowBelowMeters = thresholds && Number.isFinite(thresholds.lowBelowMeters)
      ? thresholds.lowBelowMeters
      : 100;
    const cautionBelowMeters = thresholds && Number.isFinite(thresholds.cautionBelowMeters)
      ? thresholds.cautionBelowMeters
      : 300;
    if (value < lowBelowMeters) {
      return "danger";
    }
    if (value < cautionBelowMeters) {
      return "warning";
    }
    return "safe";
  }

  function classifyGroundCollision(actionDefinition, telemetry) {
    const thresholds = actionDefinition.thresholds || {};
    const radarAltitudeMeters = numberOrNull(telemetry.radarAltitudeMeters);
    const verticalSpeedMps = numberOrNull(telemetry.verticalSpeedMps);
    const groundClosure = collisionClosureRate(
      radarAltitudeMeters,
      verticalSpeedMps,
      telemetry,
      thresholds
    );
    const groundClosureRateMps = groundClosure.groundClosureRateMps;
    const derivedSinkRateMps = numberOrNull(telemetry.derivedSinkRateMps);
    const iasKmh = numberOrNull(telemetry.iasKmh);
    const tasKmh = numberOrNull(telemetry.tasKmh);
    const gearPercent = numberOrNull(telemetry.gearPercent);
    const speedKmh = maxNumber([tasKmh, iasKmh]);
    const speedMps = speedKmh === null ? 0 : Math.max(0, speedKmh / 3.6);
    const pullAccel = (thresholdNumber(thresholds, "recoveryG", 3.5) - 1) * 9.81;
    const reactionTime = thresholdNumber(thresholds, "reactionTimeSec", 1.1);
    const speedMargin = clamp(speedMps * 0.08, 8, 28);
    const requiredAltitudeMeters = groundClosureRateMps * reactionTime +
      (groundClosureRateMps * groundClosureRateMps) / (2 * Math.max(pullAccel, 0.1)) +
      speedMargin;
    const timeToImpactSec = radarAltitudeMeters === null || groundClosureRateMps <= 0.1
      ? Infinity
      : radarAltitudeMeters / groundClosureRateMps;
    const riskRatio = radarAltitudeMeters === null
      ? 0
      : requiredAltitudeMeters / Math.max(radarAltitudeMeters, 1);
    const descentAngleDeg = Math.atan2(groundClosureRateMps, Math.max(speedMps, 20)) *
      180 / Math.PI;
    const warningRiskRatio = thresholdNumber(thresholds, "warningRiskRatio", 0.65);
    const dangerRiskRatio = thresholdNumber(thresholds, "dangerRiskRatio", 1);
    const pullUpRiskRatio = thresholdNumber(thresholds, "pullUpRiskRatio", 1.25);
    const warningTimeToImpactSec = thresholdNumber(thresholds, "warningTimeToImpactSec", 8);
    const dangerTimeToImpactSec = thresholdNumber(thresholds, "dangerTimeToImpactSec", 2.4);
    const pullUpTimeToImpactSec = thresholdNumber(thresholds, "pullUpTimeToImpactSec", 1.6);
    const minimumClosureRateMps = thresholdNumber(thresholds, "minimumClosureRateMps", 1.2);
    const minimumDescentAngleDeg = thresholdNumber(thresholds, "minimumDescentAngleDeg", 2.5);
    let rawState = "safe";
    let suppressedReason = groundClosure.suppressedReason;

    if (radarAltitudeMeters === null) {
      groundCollisionMemoryByAction.delete(actionDefinition.id);
      return {
        stateKey: "safe",
        rawState: "safe",
        verticalSpeedMps: verticalSpeedMps,
        groundClosureRateMps: groundClosureRateMps,
        derivedSinkRateMps: derivedSinkRateMps,
        speedMps: speedMps,
        riskRatio: riskRatio,
        requiredAltitudeMeters: requiredAltitudeMeters,
        timeToImpactSec: timeToImpactSec,
        descentAngleDeg: descentAngleDeg,
        suppressedReason: "no-radar-altitude"
      };
    } else if (isParkedOnGround(radarAltitudeMeters, iasKmh)) {
      suppressedReason = "parked";
    } else if (isNormalTakeoff(radarAltitudeMeters, verticalSpeedMps, iasKmh)) {
      suppressedReason = "takeoff";
    } else if (
      isNormalLanding(gearPercent, iasKmh, radarAltitudeMeters, groundClosureRateMps, descentAngleDeg) &&
      !isBadLandingRisk(groundClosureRateMps, timeToImpactSec, riskRatio)
    ) {
      suppressedReason = "landing";
    } else if (groundClosureRateMps >= minimumClosureRateMps) {
      if (riskRatio >= pullUpRiskRatio || timeToImpactSec <= pullUpTimeToImpactSec) {
        rawState = "pullUp";
      } else if (
        (riskRatio >= dangerRiskRatio || timeToImpactSec <= dangerTimeToImpactSec) &&
        descentAngleDeg >= Math.min(2, minimumDescentAngleDeg)
      ) {
        rawState = "danger";
      } else if (
        riskRatio >= warningRiskRatio &&
        timeToImpactSec <= warningTimeToImpactSec &&
        descentAngleDeg >= minimumDescentAngleDeg
      ) {
        rawState = "warning";
      }
    }

    return {
      stateKey: applyGroundCollisionHysteresis(
        actionDefinition.id,
        rawState,
        telemetry.readAt,
        thresholds
      ),
      rawState: rawState,
      verticalSpeedMps: verticalSpeedMps,
      groundClosureRateMps: groundClosureRateMps,
      derivedSinkRateMps: derivedSinkRateMps,
      speedMps: speedMps,
      riskRatio: riskRatio,
      requiredAltitudeMeters: requiredAltitudeMeters,
      timeToImpactSec: timeToImpactSec,
      descentAngleDeg: descentAngleDeg,
      suppressedReason: suppressedReason
    };
  }

  function applyGroundCollisionHysteresis(actionId, rawState, readAt, thresholds) {
    const now = Number.isFinite(readAt) ? readAt : Date.now();
    const warningPersistenceMs = thresholdNumber(thresholds, "warningPersistenceMs", 300);
    const safeClearMs = thresholdNumber(thresholds, "safeClearMs", 1200);
    const deescalateMs = thresholdNumber(thresholds, "deescalateMs", 600);
    let memory = groundCollisionMemoryByAction.get(actionId);
    if (!memory) {
      memory = {
        state: "safe",
        pendingState: "",
        pendingSince: 0,
        pendingSamples: 0,
        pendingReadAt: 0,
        safeSince: 0
      };
      groundCollisionMemoryByAction.set(actionId, memory);
    }

    const currentState = memory.state || "safe";
    if (rawState === currentState) {
      memory.pendingState = "";
      memory.pendingSamples = 0;
      memory.safeSince = rawState === "safe" ? now : 0;
      return currentState;
    }

    if (rawState === "safe") {
      if (!memory.safeSince) {
        memory.safeSince = now;
      }
      if (now - memory.safeSince >= safeClearMs) {
        memory.state = "safe";
        memory.pendingState = "";
        memory.pendingSamples = 0;
      }
      return memory.state;
    }

    memory.safeSince = 0;
    if (memory.pendingState !== rawState) {
      memory.pendingState = rawState;
      memory.pendingSince = now;
      memory.pendingSamples = 0;
      memory.pendingReadAt = 0;
    }
    if (memory.pendingReadAt !== now) {
      memory.pendingSamples += 1;
      memory.pendingReadAt = now;
    }

    const persisted = memory.pendingSamples >= 2 || now - memory.pendingSince >= warningPersistenceMs;
    const rawSeverity = collisionSeverity(rawState);
    const currentSeverity = collisionSeverity(currentState);
    if (rawSeverity > currentSeverity && (currentSeverity > 0 || rawState === "pullUp" || persisted)) {
      memory.state = rawState;
      memory.pendingState = "";
      memory.pendingSamples = 0;
      return memory.state;
    }
    if (rawSeverity < currentSeverity && now - memory.pendingSince >= deescalateMs) {
      memory.state = rawState;
      memory.pendingState = "";
      memory.pendingSamples = 0;
    }
    return memory.state;
  }

  function isParkedOnGround(radarAltitudeMeters, iasKmh) {
    return radarAltitudeMeters < 3 && iasKmh !== null && iasKmh < 80;
  }

  function collisionClosureRate(radarAltitudeMeters, verticalSpeedMps, telemetry, thresholds) {
    const radarClosureRateMps = Math.max(0, numberOrDefault(telemetry.groundClosureRateMps, 0));
    const telemetryVerticalSinkRateMps = numberOrNull(telemetry.verticalSinkRateMps);
    let verticalSinkRateMps = telemetryVerticalSinkRateMps === null
      ? null
      : Math.max(0, telemetryVerticalSinkRateMps);
    if (verticalSinkRateMps === null && verticalSpeedMps !== null) {
      verticalSinkRateMps = Math.max(0, -verticalSpeedMps);
    }

    if (radarAltitudeMeters === null || verticalSinkRateMps === null) {
      return {
        groundClosureRateMps: radarClosureRateMps,
        suppressedReason: ""
      };
    }

    const radarClosureAssistBelowMeters = thresholdNumber(
      thresholds,
      "radarClosureAssistBelowMeters",
      250
    );
    const radarClosureAssistMinSinkRateMps = thresholdNumber(
      thresholds,
      "radarClosureAssistMinSinkRateMps",
      5
    );
    const radarClosureExcessMps = radarClosureRateMps - verticalSinkRateMps;

    if (
      radarAltitudeMeters > radarClosureAssistBelowMeters &&
      verticalSinkRateMps < radarClosureAssistMinSinkRateMps &&
      radarClosureExcessMps > 0
    ) {
      return {
        groundClosureRateMps: verticalSinkRateMps,
        suppressedReason: "terrain-slope"
      };
    }

    return {
      groundClosureRateMps: Math.max(radarClosureRateMps, verticalSinkRateMps),
      suppressedReason: ""
    };
  }

  function isNormalTakeoff(radarAltitudeMeters, verticalSpeedMps, iasKmh) {
    return radarAltitudeMeters < 30 &&
      (verticalSpeedMps === null || verticalSpeedMps >= -0.5) &&
      (iasKmh === null || iasKmh < 280);
  }

  function isNormalLanding(gearPercent, iasKmh, radarAltitudeMeters, closureRateMps, descentAngleDeg) {
    return gearPercent !== null &&
      gearPercent >= 95 &&
      iasKmh !== null &&
      iasKmh < 330 &&
      radarAltitudeMeters < 120 &&
      closureRateMps < 5 &&
      descentAngleDeg < 4;
  }

  function isBadLandingRisk(closureRateMps, timeToImpactSec, riskRatio) {
    return closureRateMps >= 7 || timeToImpactSec <= 2.2 || riskRatio >= 1.15;
  }

  function isGroundCollisionAlertState(stateKey) {
    return stateKey === "warning" || stateKey === "danger" || stateKey === "pullUp";
  }

  function collisionSeverity(stateKey) {
    return Number.isFinite(COLLISION_SEVERITY[stateKey]) ? COLLISION_SEVERITY[stateKey] : 0;
  }

  function classifyDrogueReadiness(iasKmh, radarAltitudeMeters, thresholds) {
    const maxReadyIasKmh = thresholds && Number.isFinite(thresholds.maxReadyIasKmh)
      ? thresholds.maxReadyIasKmh
      : 350;
    const maxReadyRadarAltitudeMeters = thresholds &&
      Number.isFinite(thresholds.maxReadyRadarAltitudeMeters)
      ? thresholds.maxReadyRadarAltitudeMeters
      : 10;
    if (radarAltitudeMeters > maxReadyRadarAltitudeMeters) {
      return "air";
    }
    if (iasKmh > maxReadyIasKmh) {
      return "fast";
    }
    return "on";
  }

  function drogueStateTextFor(actionDefinition, stateKey) {
    const states = actionDefinition.states || {};
    if (states[stateKey]) {
      return states[stateKey];
    }
    if (stateKey === "fast") {
      return "FAST";
    }
    if (stateKey === "air") {
      return "AIR";
    }
    return indicatorStateTextFor(actionDefinition, stateKey);
  }

  function indicatorStateTextFor(actionDefinition, stateKey) {
    const states = actionDefinition.states || {};
    if (states[stateKey]) {
      return states[stateKey];
    }
    return stateKey.toUpperCase();
  }

  function toneForIndicatorState(stateKey) {
    if (stateKey === "danger" || stateKey === "pullUp") {
      return "danger";
    }
    if (stateKey === "warning") {
      return "warning";
    }
    if (stateKey === "safe") {
      return "safe";
    }
    return "offline";
  }

  function formatSignedG(value) {
    const sign = value < 0 ? "-" : "+";
    return sign + Math.abs(value).toFixed(1) + "G";
  }

  function formatSpeed(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "---";
    }
    return String(Math.max(0, Math.round(value)));
  }

  function formatFuelMass(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "----";
    }
    if (value < 100) {
      return value.toFixed(1) + "KG";
    }
    return String(Math.round(value)) + "KG";
  }

  function formatFuelPercent(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--%";
    }
    return String(Math.round(clamp(value, 0, 100))) + "%";
  }

  function formatFuelEta(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--:--";
    }
    const totalSeconds = Math.max(0, Math.round(value));
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    if (minutes > 99) {
      return "99M+";
    }
    return String(minutes) + ":" + (seconds < 10 ? "0" : "") + String(seconds);
  }

  function formatFuelBurn(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--KG/M";
    }
    const kgPerMinute = Math.max(0, value * 60);
    if (kgPerMinute < 10) {
      return kgPerMinute.toFixed(1) + "KG/M";
    }
    return String(Math.round(kgPerMinute)) + "KG/M";
  }

  function formatMeters(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "----";
    }
    return String(Math.round(value)) + "M";
  }

  function formatClosureRate(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--";
    }
    return String(Math.round(Math.max(0, value))) + "M/S";
  }

  function formatTimeToImpact(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--";
    }
    if (value < 10) {
      return value.toFixed(1) + "S";
    }
    if (value < 100) {
      return String(Math.round(value)) + "S";
    }
    return "--";
  }

  function formatAngle(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--";
    }
    return String(Math.round(Math.max(0, value))) + "DEG";
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

  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }

  function blinkOn(readAt) {
    const timestamp = Number.isFinite(readAt) ? readAt : Date.now();
    return Math.floor(timestamp / 400) % 2 === 0;
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

  function toneForFuelState(stateKey) {
    if (stateKey === "warning" || stateKey === "acknowledged") {
      return "warning";
    }
    if (stateKey === "safe") {
      return "safe";
    }
    if (stateKey === "calibrating") {
      return "transit";
    }
    return "offline";
  }

  function usesFlightValidityModel(actionDefinition) {
    const telemetry = actionDefinition.telemetry || {};
    return telemetry.mode === "flight-valid" || actionDefinition.kind === "momentary-command";
  }

  function isIndicatorAction(actionDefinition) {
    return actionDefinition.kind === "indicator" || actionDefinition.kind === "readout";
  }

  window.WTDeckStateMachines = {
    classifyPercent: classifyPercent,
    classifyGForce: classifyGForce,
    classifyGroundCollision: classifyGroundCollision,
    classifyDrogueReadiness: classifyDrogueReadiness,
    modelForAction: modelForAction
  };
})();
