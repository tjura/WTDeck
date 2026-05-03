(function () {
  const PALETTE = {
    safe: { accent: "#46d982", lamp: "#8affb9", background: "#141915" },
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
      renderStateRail(status, palette, model.statusKey),
      renderControl(actionDefinition, palette, model.statusKey, percent),
      '</svg>'
    ].join("");

    return "data:image/svg+xml;base64," + base64(svg);
  }

  function renderStateRail(status, palette, statusKey) {
    return [
      '<rect x="20" y="38" width="28" height="88" rx="5" fill="#050607" stroke="' + palette.accent + '" stroke-width="1"/>',
      '<circle cx="34" cy="50" r="5.5" fill="' + palette.lamp + '" filter="url(#glow)" opacity="' + lampOpacity(statusKey) + '"/>',
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

  function renderControl(actionDefinition, palette, statusKey, percent) {
    if (actionDefinition.id === "airbrake") {
      return renderAirbrakeControl(palette, controlPosition(statusKey, percent));
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

  function lampOpacity(statusKey) {
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
