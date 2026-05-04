(function () {
  const PALETTE = {
    safe: { accent: "#46d982", lamp: "#8affb9", background: "#141915" },
    warning: { accent: "#f2b84b", lamp: "#ffd477", background: "#1b1710" },
    danger: { accent: "#ff453a", lamp: "#ff8a80", background: "#1c1010" },
    transit: { accent: "#f2b84b", lamp: "#ffd477", background: "#1b1710" },
    dark: { accent: "#7f8a90", lamp: "#2e3538", background: "#111417" },
    offline: { accent: "#565f68", lamp: "#20252b", background: "#111317" }
  };

  function render(actionDefinition, model) {
    const palette = PALETTE[model.tone] || PALETTE.offline;
    const label = actionDefinition.panelLabel || actionDefinition.shortLabel || "";
    const status = model.statusText || "";
    const percent = typeof model.percent === "number" ? model.percent : null;

    const svg = [
      '<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">',
      '<defs>',
      '<linearGradient id="case" x1="0" y1="0" x2="1" y2="1">',
      '<stop offset="0" stop-color="#252b2f"/>',
      '<stop offset="1" stop-color="#060708"/>',
      '</linearGradient>',
      '<linearGradient id="plate" x1="0" y1="0" x2="0" y2="1">',
      '<stop offset="0" stop-color="' + palette.background + '"/>',
      '<stop offset="1" stop-color="#070809"/>',
      '</linearGradient>',
      '<filter id="glow" x="-50%" y="-50%" width="200%" height="200%">',
      '<feGaussianBlur stdDeviation="2.2" result="blur"/>',
      '<feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>',
      '</filter>',
      '</defs>',
      '<rect width="144" height="144" rx="8" fill="url(#case)"/>',
      '<rect x="8" y="8" width="128" height="128" rx="6" fill="url(#plate)" stroke="#3a4349" stroke-width="1.5"/>',
      screw(18, 18),
      screw(126, 18),
      screw(18, 126),
      screw(126, 126),
      '<text x="72" y="27" text-anchor="middle" fill="#d9dde0" font-size="12" font-family="Arial, sans-serif" font-weight="700">' + escapeXml(label) + '</text>',
      renderBody(actionDefinition, palette, model, percent),
      '</svg>'
    ].join("");

    return "data:image/svg+xml;base64," + base64(svg);
  }

  function renderBody(actionDefinition, palette, model, percent) {
    if (actionDefinition.id === "speed") {
      return renderSpeedIndicator(palette, model);
    }
    return renderStateRail(model.statusText || "", palette, model) +
      renderControl(actionDefinition, palette, model, percent);
  }

  function renderStateRail(status, palette, model) {
    const statusKey = model.statusKey;
    return [
      '<rect x="20" y="38" width="28" height="88" rx="5" fill="#050607" stroke="' + palette.accent + '" stroke-width="1"/>',
      '<circle cx="34" cy="50" r="5.5" fill="' + palette.lamp + '" filter="url(#glow)" opacity="' + lampOpacity(statusKey, model) + '"/>',
      renderVerticalStatus(status, palette)
    ].join("");
  }

  function renderVerticalStatus(status, palette) {
    const letters = compactStatus(status).split("");
    if (letters.length === 0) {
      return "";
    }

    const fontSize = letters.length <= 4 ? 16 : letters.length <= 6 ? 12 : 10;
    const gap = letters.length <= 4 ? 17 : letters.length <= 6 ? 12 : 9.5;
    const startY = 86 - ((letters.length - 1) * gap) / 2;

    return letters
      .map((letter, index) => {
        const y = Math.round((startY + index * gap) * 10) / 10;
        return '<text x="34" y="' + y + '" text-anchor="middle" dominant-baseline="middle" fill="' + palette.accent + '" font-size="' + fontSize + '" font-family="Arial, sans-serif" font-weight="800">' + escapeXml(letter) + '</text>';
      })
      .join("");
  }

  function renderControl(actionDefinition, palette, model, percent) {
    const statusKey = model.statusKey;
    if (actionDefinition.id === "gForce") {
      return renderGForceIndicator(palette, model);
    }
    if (actionDefinition.id === "altitude") {
      return renderAltitudeIndicator(palette, model);
    }
    if (actionDefinition.id === "fuel") {
      return renderFuelIndicator(palette, model);
    }
    if (actionDefinition.id === "airbrake") {
      return renderAirbrakeControl(palette, controlPosition(statusKey, percent));
    }
    if (actionDefinition.id === "flapsUp" || actionDefinition.id === "flapsDown") {
      return renderFlapsControl(
        palette,
        flapsPosition(statusKey, percent),
        actionDefinition.id === "flapsUp" ? "up" : "down",
        statusKey,
        percent
      );
    }
    if (actionDefinition.id === "drogueChute") {
      return renderDrogueChuteControl(palette, statusKey);
    }
    if (actionDefinition.id === "flares") {
      return renderFlaresControl(palette, statusKey);
    }
    if (actionDefinition.id === "chaff") {
      return renderChaffControl(palette, statusKey);
    }
    return renderGearSwitch(palette, controlPosition(statusKey, percent));
  }

  function renderGearSwitch(palette, y) {
    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<rect x="72" y="48" width="36" height="72" rx="18" fill="#050607" stroke="#3d454a" stroke-width="1.5"/>',
      '<path d="M90 61v46" stroke="#161b1e" stroke-width="10" stroke-linecap="round"/>',
      '<circle cx="90" cy="' + y + '" r="16" fill="' + palette.accent + '" filter="url(#glow)"/>',
      '<circle cx="90" cy="' + y + '" r="7" fill="#f9fbfc" opacity="0.58"/>'
    ].join("");
  }

  function renderAirbrakeControl(palette, y) {
    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<path d="M66 104l48-18M66 88l48-18M66 72l48-18" stroke="' + palette.accent + '" stroke-width="3" stroke-linecap="round" opacity="0.28"/>',
      '<rect x="62" y="48" width="56" height="72" rx="8" fill="none" stroke="#3d454a" stroke-width="1.5"/>',
      '<path d="M64 52h52M64 116h52" stroke="#59636b" stroke-width="1"/>',
      '<rect x="68" y="' + (y - 10) + '" width="44" height="20" rx="5" fill="' + palette.accent + '" filter="url(#glow)"/>',
      '<path d="M76 ' + y + 'h28" stroke="#f9fbfc" stroke-width="4" stroke-linecap="round" opacity="0.58"/>'
    ].join("");
  }

  function renderFlapsControl(palette, y, direction, statusKey, percent) {
    const activeOpacity = statusKey === "unknown" ? "0.42" : "1";
    const glowOpacity = statusKey === "unknown" ? "0.16" : "0.52";
    const percentText = formatPercent(percent);
    const percentFontSize = percentText.length >= 4 ? 11 : 13;
    const arrow = direction === "up"
      ? '<path d="M111 52l-7 10h14z" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + activeOpacity + '"/><path d="M111 63v12" stroke="' + palette.accent + '" stroke-width="3.2" stroke-linecap="round" opacity="' + glowOpacity + '"/>'
      : '<path d="M111 114l-7-10h14z" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + activeOpacity + '"/><path d="M111 91v12" stroke="' + palette.accent + '" stroke-width="3.2" stroke-linecap="round" opacity="' + glowOpacity + '"/>';

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<rect x="62" y="48" width="35" height="66" rx="6" fill="#050607" stroke="#3d454a" stroke-width="1.5"/>',
      '<path d="M73 56v48" stroke="#161b1e" stroke-width="8" stroke-linecap="round"/>',
      '<path d="M73 56v' + Math.max(0, Math.round((y - 56) * 10) / 10) + '" stroke="' + palette.accent + '" stroke-width="5" stroke-linecap="round" opacity="' + glowOpacity + '"/>',
      '<path d="M66 56h15M68 68h11M66 80h15M68 92h11M66 104h15" stroke="#59636b" stroke-width="1.3" stroke-linecap="round" opacity="' + activeOpacity + '"/>',
      '<text x="85" y="59" text-anchor="start" fill="#8f9aa1" font-size="7" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">0</text>',
      '<text x="84" y="83" text-anchor="start" fill="#8f9aa1" font-size="7" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">50</text>',
      '<text x="82" y="107" text-anchor="start" fill="#8f9aa1" font-size="7" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">100</text>',
      '<rect x="62" y="' + (y - 5) + '" width="22" height="10" rx="5" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + activeOpacity + '"/>',
      '<path d="M66 ' + y + 'h14" stroke="#f9fbfc" stroke-width="2.2" stroke-linecap="round" opacity="0.48"/>',
      '<rect x="96" y="77" width="25" height="18" rx="4" fill="#0c1113" stroke="#344047" stroke-width="1"/>',
      '<text x="108.5" y="90" text-anchor="middle" fill="' + palette.accent + '" font-size="' + percentFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + activeOpacity + '">' + escapeXml(percentText) + '</text>',
      arrow
    ].join("");
  }

  function renderGForceIndicator(palette, model) {
    const valueText = model.valueText || "--.-G";
    const activeOpacity = model.statusKey === "danger" && model.blinkOn === false ? "0.22" : "1";
    const glowOpacity = model.statusKey === "danger" && model.blinkOn === false ? "0.12" : "0.36";
    const valueFontSize = valueText.length > 5 ? 18 : 21;

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<path d="M66 104h46" stroke="#59636b" stroke-width="1.5" stroke-linecap="round"/>',
      '<path d="M72 104v-6M84 104v-10M96 104v-10M108 104v-6" stroke="#59636b" stroke-width="1.5" stroke-linecap="round"/>',
      '<path d="M70 96c5-15 17-23 34-25" fill="none" stroke="' + palette.accent + '" stroke-width="3" stroke-linecap="round" opacity="' + glowOpacity + '"/>',
      '<text x="89" y="82" text-anchor="middle" fill="' + palette.accent + '" font-size="' + valueFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + activeOpacity + '">' + escapeXml(valueText) + '</text>',
      '<circle cx="89" cy="105" r="7" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + activeOpacity + '"/>'
    ].join("");
  }

  function renderAltitudeIndicator(palette, model) {
    const radarText = model.radarAltitudeText || "----";
    const altitudeText = model.altitudeText || "----";
    const active = model.statusKey !== "unknown";
    const alerting = model.statusKey === "warning" ||
      model.statusKey === "danger" ||
      model.statusKey === "pullUp";
    const blinkingOff = alerting && model.blinkOn === false;
    const activeOpacity = !active ? "0.34" : blinkingOff ? "0.22" : "1";
    const radarOpacity = !active ? activeOpacity : blinkingOff ? "0.22" : model.radarAltitudeMeters === null ? "0.42" : "1";
    const altitudeOpacity = !active ? activeOpacity : model.altitudeMeters === null ? "0.42" : "1";
    const radarFontSize = readoutFontSize(radarText, 23);
    const altitudeFontSize = readoutFontSize(altitudeText, 17);

    if (alerting) {
      return renderGroundCollisionPanel(
        palette,
        model,
        radarText,
        radarOpacity,
        activeOpacity
      );
    }

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<path d="M62 69h54M62 102h54" stroke="#263139" stroke-width="1"/>',
      '<path d="M65 58h48" stroke="' + palette.accent + '" stroke-width="2" stroke-linecap="round" opacity="' + (active ? "0.28" : "0.12") + '"/>',
      '<path d="M66 112c8-8 15-10 24-7s16 1 25-8" fill="none" stroke="' + palette.accent + '" stroke-width="2" stroke-linecap="round" opacity="' + (active ? "0.30" : "0.12") + '"/>',
      '<text x="64" y="53" text-anchor="start" fill="#8f9aa1" font-size="8" font-family="Arial, sans-serif" font-weight="700">RALT</text>',
      '<text x="89" y="80" text-anchor="middle" fill="' + palette.accent + '" font-size="' + radarFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + radarOpacity + '">' + escapeXml(radarText) + '</text>',
      '<text x="64" y="96" text-anchor="start" fill="#8f9aa1" font-size="8" font-family="Arial, sans-serif" font-weight="700">ALT</text>',
      '<text x="91" y="117" text-anchor="middle" fill="#d9dde0" font-size="' + altitudeFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + altitudeOpacity + '">' + escapeXml(altitudeText) + '</text>'
    ].join("");
  }

  function renderFuelIndicator(palette, model) {
    const etaText = model.fuelEtaText || "--:--";
    const massText = model.fuelMassText || "----";
    const percentText = model.fuelPercentText || "--%";
    const burnText = model.fuelBurnText || "--KG/M";
    const active = model.statusKey !== "unknown";
    const warning = model.lowFuelWarningActive === true;
    const acknowledged = model.statusKey === "acknowledged";
    const blinkingOff = warning && !acknowledged && model.blinkOn === false;
    const activeOpacity = !active ? "0.34" : blinkingOff ? "0.26" : "1";
    const glowOpacity = !active ? "0.12" : warning ? "0.62" : "0.32";
    const fillPercent = typeof model.fuelPercent === "number"
      ? clamp(model.fuelPercent, 0, 100)
      : null;
    const fillHeight = fillPercent === null ? 0 : Math.round((fillPercent / 100) * 54);
    const etaFontSize = readoutFontSize(etaText, 27);
    const massFontSize = readoutFontSize(massText, 14);
    const percentOpacity = fillPercent === null ? "0.42" : activeOpacity;

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<rect x="62" y="48" width="15" height="60" rx="4" fill="#050607" stroke="#3d454a" stroke-width="1.3" opacity="' + activeOpacity + '"/>',
      '<rect x="65" y="' + (105 - fillHeight) + '" width="9" height="' + fillHeight + '" rx="3" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + percentOpacity + '"/>',
      '<path d="M64 57h11M64 70h11M64 83h11M64 96h11" stroke="#59636b" stroke-width="1" opacity="' + (active ? "0.58" : "0.22") + '"/>',
      '<path d="M85 55h27" stroke="' + palette.accent + '" stroke-width="2.2" stroke-linecap="round" filter="url(#glow)" opacity="' + glowOpacity + '"/>',
      '<text x="99" y="73" text-anchor="middle" fill="' + palette.accent + '" font-size="' + etaFontSize + '" font-family="Arial, sans-serif" font-weight="900" opacity="' + activeOpacity + '">' + escapeXml(etaText) + '</text>',
      '<text x="99" y="85" text-anchor="middle" fill="#8f9aa1" font-size="7.5" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">ENDUR</text>',
      '<rect x="82" y="93" width="35" height="14" rx="3" fill="#0c1113" stroke="#344047" stroke-width="1" opacity="' + activeOpacity + '"/>',
      '<text x="99.5" y="104" text-anchor="middle" fill="#d9dde0" font-size="' + massFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + activeOpacity + '">' + escapeXml(massText) + '</text>',
      '<text x="69.5" y="119" text-anchor="middle" fill="' + palette.accent + '" font-size="9" font-family="Arial, sans-serif" font-weight="800" opacity="' + percentOpacity + '">' + escapeXml(percentText) + '</text>',
      '<text x="101" y="119" text-anchor="middle" fill="#8f9aa1" font-size="7.5" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">' + escapeXml(burnText) + '</text>'
    ].join("");
  }

  function renderGroundCollisionPanel(palette, model, radarText, radarOpacity, activeOpacity) {
    const statusText = model.statusText || "";
    const pullUp = model.statusKey === "pullUp";
    const ttiText = model.timeToImpactText || "--";
    const closureText = model.closureRateText || "--";
    const terrainFill = model.statusKey === "warning" ? "#2c220d" : "#2a0d0d";
    const terrainStroke = model.statusKey === "warning" ? "#6d5320" : "#6f221f";

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="' + terrainStroke + '" stroke-width="1.2"/>',
      '<path d="M60 106l12-15 9 9 12-20 9 13 15-24v37z" fill="' + terrainFill + '" stroke="' + palette.accent + '" stroke-width="1.6" opacity="' + activeOpacity + '"/>',
      '<path d="M62 57h54" stroke="' + palette.accent + '" stroke-width="2.2" stroke-linecap="round" opacity="' + (model.statusKey === "warning" ? "0.45" : "0.70") + '" filter="url(#glow)"/>',
      '<text x="63" y="53" text-anchor="start" fill="#8f9aa1" font-size="8" font-family="Arial, sans-serif" font-weight="700">RALT</text>',
      '<text x="99" y="54" text-anchor="middle" fill="' + palette.accent + '" font-size="' + readoutFontSize(radarText, 14) + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + radarOpacity + '">' + escapeXml(radarText) + '</text>',
      pullUp ? "" : '<text x="89" y="71" text-anchor="middle" fill="#d9dde0" font-size="7.5" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">SINK ' + escapeXml(closureText) + '</text>',
      pullUp
        ? '<text x="89" y="82" text-anchor="middle" fill="' + palette.accent + '" font-size="18" font-family="Arial, sans-serif" font-weight="900" opacity="' + activeOpacity + '">PULL</text><text x="89" y="100" text-anchor="middle" fill="' + palette.accent + '" font-size="18" font-family="Arial, sans-serif" font-weight="900" opacity="' + activeOpacity + '">UP</text>'
        : '<text x="89" y="86" text-anchor="middle" fill="' + palette.accent + '" font-size="13" font-family="Arial, sans-serif" font-weight="900" opacity="' + activeOpacity + '">' + escapeXml(statusText) + '</text>',
      '<rect x="61" y="108" width="56" height="13" rx="3" fill="#0c1113" stroke="#344047" stroke-width="1" opacity="' + activeOpacity + '"/>',
      '<text x="67" y="118" text-anchor="start" fill="#8f9aa1" font-size="7.5" font-family="Arial, sans-serif" font-weight="700">TTI</text>',
      '<text x="112" y="118" text-anchor="end" fill="#d9dde0" font-size="8.5" font-family="Arial, sans-serif" font-weight="800">' + escapeXml(ttiText) + '</text>'
    ].join("");
  }

  function renderSpeedIndicator(palette, model) {
    const iasText = model.iasText || "---";
    const tasText = model.tasText || "---";
    const statusText = model.statusText || "";
    const active = model.statusKey === "safe";
    const activeOpacity = active ? "1" : "0.36";
    const needleAngle = speedNeedleAngle(model.iasValue);
    const valueFontSize = iasText.length >= 4 ? 28 : 32;
    const statusFill = active ? "#8f9aa1" : palette.accent;

    return [
      '<circle cx="72" cy="76" r="48" fill="#050607" stroke="#3d454a" stroke-width="2"/>',
      '<circle cx="72" cy="76" r="41" fill="#080d0f" stroke="#20282d" stroke-width="1"/>',
      '<path d="M36 93a40 40 0 1 1 72 0" fill="none" stroke="' + palette.accent + '" stroke-width="3" stroke-linecap="round" opacity="' + (active ? "0.32" : "0.12") + '"/>',
      renderSpeedTicks(palette, active),
      '<path d="M72 76l0-31" stroke="' + palette.lamp + '" stroke-width="3" stroke-linecap="round" filter="url(#glow)" opacity="' + activeOpacity + '" transform="rotate(' + needleAngle + ' 72 76)"/>',
      '<circle cx="72" cy="76" r="5" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + activeOpacity + '"/>',
      '<text x="72" y="70" text-anchor="middle" fill="' + palette.accent + '" font-size="' + valueFontSize + '" font-family="Arial, sans-serif" font-weight="800" opacity="' + activeOpacity + '">' + escapeXml(iasText) + '</text>',
      '<text x="72" y="88" text-anchor="middle" fill="#d9dde0" font-size="8" font-family="Arial, sans-serif" font-weight="700" opacity="' + activeOpacity + '">IAS KM/H</text>',
      '<rect x="39" y="103" width="66" height="18" rx="4" fill="#0c1113" stroke="#344047" stroke-width="1"/>',
      '<text x="48" y="116" text-anchor="middle" fill="#8f9aa1" font-size="8" font-family="Arial, sans-serif" font-weight="700">TAS</text>',
      '<text x="91" y="116" text-anchor="middle" fill="#d9dde0" font-size="13" font-family="Arial, sans-serif" font-weight="800" opacity="' + activeOpacity + '">' + escapeXml(tasText) + '</text>',
      '<text x="72" y="130" text-anchor="middle" fill="' + statusFill + '" font-size="8" font-family="Arial, sans-serif" font-weight="700">' + escapeXml(statusText) + '</text>'
    ].join("");
  }

  function renderSpeedTicks(palette, active) {
    const angles = [-125, -95, -65, -35, -5, 25, 55, 85, 115, 125];
    return angles
      .map((angle, index) => {
        const major = index % 3 === 0 || index === angles.length - 1;
        return speedTick(
          angle,
          major ? 9 : 5,
          major ? 1.7 : 1.2,
          major ? palette.accent : "#59636b",
          active ? (major ? "0.82" : "0.56") : "0.22"
        );
      })
      .join("");
  }

  function speedTick(angle, length, width, color, opacity) {
    const outer = polar(72, 76, 42, angle);
    const inner = polar(72, 76, 42 - length, angle);
    return '<path d="M' + inner.x + ' ' + inner.y + 'L' + outer.x + ' ' + outer.y + '" stroke="' + color + '" stroke-width="' + width + '" stroke-linecap="round" opacity="' + opacity + '"/>';
  }

  function speedNeedleAngle(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return -125;
    }
    return Math.round(clamp((Math.max(0, value) / 1200) * 250 - 125, -125, 125));
  }

  function readoutFontSize(text, baseSize) {
    const length = String(text || "").length;
    if (length <= 4) {
      return baseSize;
    }
    if (length <= 5) {
      return baseSize - 2;
    }
    if (length <= 6) {
      return baseSize - 4;
    }
    return baseSize - 6;
  }

  function polar(cx, cy, radius, angle) {
    const radians = (angle - 90) * Math.PI / 180;
    return {
      x: Math.round((cx + Math.cos(radians) * radius) * 10) / 10,
      y: Math.round((cy + Math.sin(radians) * radius) * 10) / 10
    };
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function renderDrogueChuteControl(palette, statusKey) {
    const active = statusKey === "on" ||
      statusKey === "armed" ||
      statusKey === "brake" ||
      statusKey === "drogue" ||
      statusKey === "stopped";
    const opacity = active ? "1" : "0.5";
    const guardStroke = active ? palette.accent : "#3d454a";

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<path d="M67 51h46v56H67z" fill="#050607" stroke="' + guardStroke + '" stroke-width="1.5" opacity="' + opacity + '"/>',
      '<path d="M72 56h36M72 102h36" stroke="#59636b" stroke-width="1" opacity="' + opacity + '"/>',
      '<path d="M78 62c10-8 25-8 34 0" fill="none" stroke="' + palette.accent + '" stroke-width="3" stroke-linecap="round" opacity="0.32"/>',
      '<path d="M90 63v32" stroke="' + palette.accent + '" stroke-width="8" stroke-linecap="round" filter="url(#glow)" opacity="' + opacity + '"/>',
      '<circle cx="90" cy="100" r="16" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + opacity + '"/>',
      '<circle cx="90" cy="100" r="7" fill="#f9fbfc" opacity="0.58"/>',
      '<path d="M80 100h20" stroke="#f9fbfc" stroke-width="3" stroke-linecap="round" opacity="0.42"/>'
    ].join("");
  }

  function renderFlaresControl(palette, statusKey) {
    const opacity = statusKey === "on" ? "1" : "0.45";
    const portOpacity = statusKey === "on" ? "0.66" : "0.22";
    const flareGlow = statusKey === "on" ? "0.84" : "0.18";
    const rimStroke = statusKey === "on" ? palette.accent : "#3d454a";

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<rect x="63" y="48" width="34" height="42" rx="5" fill="#050607" stroke="' + rimStroke + '" stroke-width="1.5" opacity="' + opacity + '"/>',
      '<path d="M68 58h24M68 72h24M68 86h24" stroke="#59636b" stroke-width="1" stroke-linecap="round" opacity="0.45"/>',
      flarePort(72, 58, palette, portOpacity),
      flarePort(88, 58, palette, portOpacity),
      flarePort(72, 76, palette, portOpacity),
      flarePort(88, 76, palette, portOpacity),
      '<path d="M93 52l19-8M96 59l20 1M93 66l18 10" stroke="' + palette.accent + '" stroke-width="3" stroke-linecap="round" filter="url(#glow)" opacity="' + flareGlow + '"/>',
      '<circle cx="114" cy="44" r="2.6" fill="' + palette.lamp + '" filter="url(#glow)" opacity="' + flareGlow + '"/>',
      '<circle cx="118" cy="60" r="2.2" fill="' + palette.lamp + '" filter="url(#glow)" opacity="' + flareGlow + '"/>',
      '<rect x="64" y="96" width="50" height="22" rx="5" fill="#111618" stroke="#3d454a" stroke-width="1.5" opacity="' + opacity + '"/>',
      '<circle cx="89" cy="107" r="11" fill="#070809" stroke="' + rimStroke + '" stroke-width="1.5"/>',
      '<circle cx="89" cy="107" r="7" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + opacity + '"/>',
      '<circle cx="89" cy="107" r="3" fill="#f9fbfc" opacity="0.48"/>'
    ].join("");
  }

  function flarePort(x, y, palette, opacity) {
    return [
      '<circle cx="' + x + '" cy="' + y + '" r="5.2" fill="#0d1112" stroke="#3d454a" stroke-width="1"/>',
      '<circle cx="' + x + '" cy="' + y + '" r="2.8" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + opacity + '"/>'
    ].join("");
  }

  function renderChaffControl(palette, statusKey) {
    const opacity = statusKey === "on" ? "1" : "0.45";
    const cloudOpacity = statusKey === "on" ? "0.72" : "0.2";
    const rimStroke = statusKey === "on" ? palette.accent : "#3d454a";

    return [
      '<rect x="54" y="38" width="70" height="88" rx="5" fill="#080a0b" stroke="#323b42" stroke-width="1"/>',
      '<rect x="63" y="48" width="30" height="42" rx="5" fill="#050607" stroke="' + rimStroke + '" stroke-width="1.5" opacity="' + opacity + '"/>',
      '<path d="M69 56h18M69 66h18M69 76h18M69 86h18" stroke="#59636b" stroke-width="1.5" stroke-linecap="round" opacity="0.5"/>',
      '<path d="M91 57c7-9 17-12 27-11M92 66c8-4 17-5 27-2M92 75c8 2 17 7 25 15" fill="none" stroke="' + palette.accent + '" stroke-width="2.5" stroke-linecap="round" filter="url(#glow)" opacity="' + cloudOpacity + '"/>',
      chaffStrip(105, 50, -14, palette, cloudOpacity),
      chaffStrip(116, 58, 10, palette, cloudOpacity),
      chaffStrip(104, 72, 18, palette, cloudOpacity),
      chaffStrip(113, 84, -8, palette, cloudOpacity),
      '<rect x="64" y="96" width="50" height="22" rx="5" fill="#111618" stroke="#3d454a" stroke-width="1.5" opacity="' + opacity + '"/>',
      '<circle cx="89" cy="107" r="11" fill="#070809" stroke="' + rimStroke + '" stroke-width="1.5"/>',
      '<circle cx="89" cy="107" r="7" fill="' + palette.accent + '" filter="url(#glow)" opacity="' + opacity + '"/>',
      '<path d="M84 107h10" stroke="#f9fbfc" stroke-width="2.5" stroke-linecap="round" opacity="0.48"/>'
    ].join("");
  }

  function chaffStrip(x, y, rotate, palette, opacity) {
    return '<rect x="' + (x - 6) + '" y="' + (y - 1) + '" width="12" height="2" rx="1" fill="' + palette.lamp + '" opacity="' + opacity + '" transform="rotate(' + rotate + ' ' + x + ' ' + y + ')" filter="url(#glow)"/>';
  }

  function controlPosition(statusKey, percent) {
    if (typeof percent === "number") {
      return Math.round(104 - (percent / 100) * 40);
    }
    if (statusKey === "on") {
      return 64;
    }
    if (statusKey === "moving") {
      return 84;
    }
    return 104;
  }

  function flapsPosition(statusKey, percent) {
    if (typeof percent === "number" && Number.isFinite(percent)) {
      return Math.round((56 + (clamp(percent, 0, 100) / 100) * 48) * 10) / 10;
    }
    if (statusKey === "on") {
      return 104;
    }
    if (statusKey === "moving") {
      return 80;
    }
    if (statusKey === "off") {
      return 56;
    }
    return 80;
  }

  function formatPercent(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return "--%";
    }
    return String(Math.round(clamp(value, 0, 100))) + "%";
  }

  function lampOpacity(statusKey, model) {
    if ((statusKey === "danger" || statusKey === "pullUp") && model && model.blinkOn === false) {
      return "0.18";
    }
    if (statusKey === "warning" && model && model.lowFuelWarningActive && model.blinkOn === false) {
      return "0.22";
    }
    if (statusKey === "unknown") {
      return "0.25";
    }
    if (statusKey === "off") {
      return "0.4";
    }
    return "1";
  }

  function screw(x, y) {
    return [
      '<circle cx="' + x + '" cy="' + y + '" r="4" fill="#111416" stroke="#4a535a" stroke-width="1"/>',
      '<path d="M' + (x - 2.5) + ' ' + y + 'h5" stroke="#59636b" stroke-width="1"/>'
    ].join("");
  }

  function compactStatus(status) {
    const normalized = String(status || "").trim().toUpperCase();
    if (normalized === "NO FLIGHT") {
      return "NOFLT";
    }
    if (normalized === "OFFLINE") {
      return "OFF";
    }
    if (normalized === "TRANSIT") {
      return "TRNST";
    }
    if (normalized === "DANGER") {
      return "DNGR";
    }
    if (normalized === "TERRAIN") {
      return "TERR";
    }
    if (normalized === "PULL UP") {
      return "PULL";
    }
    if (normalized === "WARNING") {
      return "WARN";
    }
    if (normalized === "READY") {
      return "RDY";
    }
    return normalized.replace(/\s+/g, "");
  }

  function escapeXml(value) {
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&apos;");
  }

  function base64(value) {
    return btoa(unescape(encodeURIComponent(value)));
  }

  window.WTDeckKeyRenderer = {
    render: render
  };
})();
