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
    const value = model.valueText || "";
    const percent = typeof model.percent === "number" ? model.percent : null;
    const switchY = switchPosition(model.statusKey, percent);

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
      '<rect x="28" y="38" width="88" height="28" rx="4" fill="#050607" stroke="' + palette.accent + '" stroke-width="1"/>',
      '<circle cx="45" cy="52" r="7" fill="' + palette.lamp + '" filter="url(#glow)" opacity="' + lampOpacity(model.statusKey) + '"/>',
      '<text x="61" y="56" fill="' + palette.accent + '" font-size="11" font-family="Arial, sans-serif" font-weight="700">' + escapeXml(status) + '</text>',
      renderSwitch(palette, switchY),
      renderValue(value, palette),
      '</svg>'
    ].join("");

    return "data:image/svg+xml;base64," + base64(svg);
  }

  function renderSwitch(palette, y) {
    return [
      '<rect x="58" y="76" width="28" height="48" rx="14" fill="#050607" stroke="#3d454a" stroke-width="1"/>',
      '<circle cx="72" cy="' + y + '" r="12" fill="' + palette.accent + '" filter="url(#glow)"/>',
      '<circle cx="72" cy="' + y + '" r="6" fill="#f9fbfc" opacity="0.55"/>'
    ].join("");
  }

  function renderValue(value, palette) {
    if (!value) {
      return "";
    }
    return '<text x="72" y="129" text-anchor="middle" fill="#edf1f3" font-size="12" font-family="Arial, sans-serif" font-weight="700">' + escapeXml(value) + '</text>';
  }

  function switchPosition(statusKey, percent) {
    if (typeof percent === "number") {
      return Math.round(118 - (percent / 100) * 36);
    }
    if (statusKey === "on") {
      return 82;
    }
    if (statusKey === "moving") {
      return 100;
    }
    return 118;
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
