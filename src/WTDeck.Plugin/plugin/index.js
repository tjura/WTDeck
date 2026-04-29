/*
 * WTDeck StreamDock plugin - SDK v1
 *
 * Thin transport layer:
 *   - polls button state every 500ms and the information panel every 100ms
 *   - POSTs /api/actions/{actionKey} on button press
 *   - PUTs /api/stream-controller/status heartbeat every 2s
 *   - blinks buttons when the backend marks the action as blinking
 *
 * All business logic lives in WTDeck.App (.NET). This plugin only renders state.
 */

"use strict";

const API_BASE = "http://127.0.0.1:8730";
const BUTTON_POLL_INTERVAL_MS = 500;
const PANEL_POLL_INTERVAL_MS = 100;
const HEARTBEAT_INTERVAL_MS = 2000;
const BLINK_INTERVAL_MS = 500;
const BLINK_OFF_ASSET = "gear-blink-off.svg";
const PANEL_WIDTH = 192;
const PANEL_HEIGHT = 384;

const ACTIONS = {
    "com.wtdeck.streamdock.gear": {
        actionKey: "landing-gear",
        setTitle: false,
        fallbackTitle: "",
        statusToAsset: {
            up: "gear-retracted.svg",
            down: "gear-deployed.svg",
            extending: "gear-deploying.svg",
            retracting: "gear-retracting.svg",
            danger: "gear-damaged.svg",
            unavailable: "gear-disabled.svg",
            unknown: "gear-unknown.svg",
        },
    },
    "com.wtdeck.streamdock.flares": {
        actionKey: "launch-flares",
        setTitle: true,
        fallbackTitle: "NO FLARES",
        statusToAsset: {
            ready: "flare-ready.svg",
            unavailable: "flare-unavailable.svg",
            unknown: "flare-unknown.svg",
        },
    },
    "com.wtdeck.streamdock.flight-alerts": {
        actionKey: "flight-alerts",
        panel: true,
        setTitle: false,
        fallbackTitle: "",
        statusToAsset: {
            unavailable: "flight-alerts-panel.svg",
            unknown: "flight-alerts-panel.svg",
        },
    },
};

let websocket = null;
let pluginUUID = null;
const contexts = new Map(); // context -> per-context state
const assetCache = new Map(); // assetName -> dataUrl
let buttonPollId = null;
let panelPollId = null;
let heartbeatId = null;
let pollInFlight = false;
let heartbeatInFlight = false;
let lastSnapshot = null;

// Entry point required by the StreamDock host (same name as Elgato SDK v1)
function connectElgatoStreamDeckSocket(inPort, inPluginUUID, inRegisterEvent, inInfo) {
    pluginUUID = inPluginUUID;
    websocket = new WebSocket("ws://127.0.0.1:" + inPort);

    websocket.onopen = function () {
        const registerMessage = {
            event: inRegisterEvent,
            uuid: inPluginUUID,
        };
        websocket.send(JSON.stringify(registerMessage));
    };

    websocket.onmessage = function (evt) {
        const message = JSON.parse(evt.data);
        handleMessage(message);
    };

    websocket.onerror = function (err) {
        console.error("[WTDeck] WebSocket error", err);
    };

    websocket.onclose = function () {
        stopAllContexts();
    };
}

function handleMessage(message) {
    const event = message.event;
    const context = message.context;
    const action = message.action;

    if (!event) return;

    const actionDefinition = ACTIONS[action];

    if (event === "willAppear" && actionDefinition) {
        startContext(context, actionDefinition);
    } else if (event === "willDisappear" && actionDefinition) {
        stopContext(context);
    } else if (event === "keyDown" && actionDefinition && !actionDefinition.panel) {
        triggerAction(actionDefinition.actionKey);
    }
}

function createContextState(actionDefinition) {
    return {
        actionKey: actionDefinition.actionKey,
        isPanel: !!actionDefinition.panel,
        statusToAsset: actionDefinition.statusToAsset,
        shouldSetTitle: actionDefinition.setTitle,
        fallbackTitle: actionDefinition.fallbackTitle,
        blinkId: null,
        blinkPhaseOn: true,
        lastStatus: null,
        lastTitle: null,
        lastBlinking: false,
        lastPanelSignature: null,
        onImageUrl: null,  // current state's full-color image
        offImageUrl: null, // blink-off image (text only)
    };
}

function startContext(context, actionDefinition) {
    if (contexts.has(context)) return;

    const state = createContextState(actionDefinition);
    contexts.set(context, state);

    if (!state.isPanel) {
        // Preload the blink-off asset (reused across all blinking states)
        loadAsset(BLINK_OFF_ASSET).then(function (dataUrl) {
            const s = contexts.get(context);
            if (s === state) s.offImageUrl = dataUrl;
        });
    }

    if (lastSnapshot && lastSnapshot.state) {
        applyState(context, lastSnapshot);
    } else {
        syncContexts(state.isPanel ? "panel" : "button");
    }

    rebalanceTransportLoops();
}

function stopContext(context) {
    const state = contexts.get(context);
    if (!state) return;

    if (state.blinkId) clearInterval(state.blinkId);
    contexts.delete(context);

    rebalanceTransportLoops();
}

function stopAllContexts() {
    contexts.forEach(function (state) {
        if (state.blinkId) clearInterval(state.blinkId);
    });
    contexts.clear();
    stopTransportLoops();
}

function rebalanceTransportLoops() {
    if (hasButtonContexts()) {
        if (!buttonPollId) {
            buttonPollId = setInterval(function () {
                syncContexts("button");
            }, BUTTON_POLL_INTERVAL_MS);
        }
    } else if (buttonPollId) {
        clearInterval(buttonPollId);
        buttonPollId = null;
    }

    if (hasPanelContexts()) {
        if (!panelPollId) {
            panelPollId = setInterval(function () {
                syncContexts("panel");
            }, PANEL_POLL_INTERVAL_MS);
        }
    } else if (panelPollId) {
        clearInterval(panelPollId);
        panelPollId = null;
    }

    if (contexts.size > 0 && !heartbeatId) {
        sendHeartbeat("connected");
        heartbeatId = setInterval(function () {
            sendHeartbeat("connected");
        }, HEARTBEAT_INTERVAL_MS);
    }

    if (contexts.size === 0) {
        stopTransportLoops();
        sendHeartbeat("disconnected");
    }
}

function stopTransportLoops() {
    if (buttonPollId) {
        clearInterval(buttonPollId);
        buttonPollId = null;
    }

    if (panelPollId) {
        clearInterval(panelPollId);
        panelPollId = null;
    }

    if (heartbeatId) {
        clearInterval(heartbeatId);
        heartbeatId = null;
    }
}

function hasButtonContexts() {
    let found = false;
    contexts.forEach(function (state) {
        if (!state.isPanel) found = true;
    });
    return found;
}

function hasPanelContexts() {
    let found = false;
    contexts.forEach(function (state) {
        if (state.isPanel) found = true;
    });
    return found;
}

function targetContexts(kind) {
    const targets = [];
    contexts.forEach(function (state, context) {
        if ((kind === "panel" && state.isPanel) || (kind === "button" && !state.isPanel)) {
            targets.push(context);
        }
    });
    return targets;
}

function syncContexts(kind) {
    const targets = targetContexts(kind);
    if (pollInFlight || targets.length === 0) return;

    pollInFlight = true;

    fetch(API_BASE + "/api/stream-dock/state", { cache: "no-store" })
        .then(function (response) {
            if (!response.ok) throw new Error("http " + response.status);
            return response.json();
        })
        .then(function (snapshot) {
            lastSnapshot = snapshot;
            targets.forEach(function (context) {
                applyState(context, snapshot);
            });
        })
        .catch(function () {
            lastSnapshot = null;
            targets.forEach(function (context) {
                applyFallback(context);
            });
        })
        .finally(function () {
            pollInFlight = false;
        });
}

function applyState(context, snapshot) {
    const state = contexts.get(context);
    if (!state || !snapshot || !snapshot.state) return;

    if (state.isPanel) {
        applyPanelState(context, snapshot);
        return;
    }

    const actionState = getActionState(snapshot, state.actionKey);
    if (!actionState) {
        applyFallback(context);
        return;
    }

    const status = actionState.statusKey || "unknown";
    const title = typeof actionState.title === "string" ? actionState.title : "";
    const blinking = !!actionState.isBlinking;
    const statusChanged = status !== state.lastStatus;
    const displayTitle = status === "unavailable" ? "" : title;
    const titleChanged = displayTitle !== state.lastTitle;
    const blinkingChanged = blinking !== state.lastBlinking;

    if (titleChanged) {
        state.lastTitle = displayTitle;
        if (state.shouldSetTitle) setTitle(context, displayTitle);
    }

    if (statusChanged) {
        state.lastStatus = status;
        const assetName = state.statusToAsset[status] || state.statusToAsset.unknown;
        loadAsset(assetName).then(function (dataUrl) {
            const current = contexts.get(context);
            if (current !== state) return;
            current.onImageUrl = dataUrl;
            // If not blinking, show the image immediately.
            // If blinking, the interval will pick it up on the next tick.
            if (!current.lastBlinking) {
                setImage(context, dataUrl);
            } else {
                // Restart blink cycle in "on" phase so the new color appears instantly.
                current.blinkPhaseOn = true;
                setImage(context, dataUrl);
            }
        });
    }

    if (blinkingChanged) {
        state.lastBlinking = blinking;
        if (blinking) {
            startBlink(context);
        } else {
            stopBlink(context);
            if (state.onImageUrl) setImage(context, state.onImageUrl);
        }
    }
}

function applyPanelState(context, snapshot) {
    const state = contexts.get(context);
    if (!state) return;

    const model = buildPanelModel(snapshot);
    const signature = JSON.stringify(model);
    if (signature === state.lastPanelSignature) return;

    state.lastPanelSignature = signature;
    setImage(context, renderPanel(model));
}

function buildPanelModel(snapshot) {
    const root = snapshot && snapshot.state ? snapshot.state : {};
    const panel = root.panel || {};
    const alerts = root.alerts || {};
    const overG = alerts["over-g"] || {};
    const panelAvailable = panel.isAvailable !== false && overG.isAvailable === true;

    return {
        available: panelAvailable,
        statusKey: panelAvailable ? (panel.statusKey || overG.statusKey || "normal") : "unavailable",
        rows: [
            {
                label: typeof overG.label === "string" ? overG.label : "G",
                value: panelAvailable && typeof overG.value === "string" ? overG.value : "--",
                statusKey: panelAvailable ? (overG.statusKey || "normal") : "unavailable",
                active: panelAvailable,
            },
            { label: "", value: "--", statusKey: "unavailable", active: false },
            { label: "", value: "--", statusKey: "unavailable", active: false },
        ],
    };
}

function renderPanel(model) {
    const canvas = document.createElement("canvas");
    canvas.width = PANEL_WIDTH;
    canvas.height = PANEL_HEIGHT;
    const ctx = canvas.getContext("2d");
    const colors = panelColors(model.statusKey, model.available);

    ctx.fillStyle = colors.background;
    ctx.fillRect(0, 0, PANEL_WIDTH, PANEL_HEIGHT);

    const rowHeight = 104;
    const gap = 12;
    const startY = 24;
    for (let i = 0; i < 3; i++) {
        const row = model.rows[i];
        const y = startY + i * (rowHeight + gap);
        const rowColors = panelColors(row.statusKey, model.available && row.active);

        ctx.fillStyle = rowColors.row;
        roundRect(ctx, 14, y, PANEL_WIDTH - 28, rowHeight, 8);
        ctx.fill();

        ctx.fillStyle = rowColors.accent;
        roundRect(ctx, 14, y, 5, rowHeight, 4);
        ctx.fill();

        ctx.fillStyle = rowColors.label;
        ctx.font = "700 26px Arial, sans-serif";
        ctx.textBaseline = "middle";
        ctx.textAlign = "left";
        ctx.fillText(row.label, 32, y + rowHeight / 2);

        ctx.fillStyle = rowColors.value;
        ctx.font = "700 38px Arial, sans-serif";
        ctx.textAlign = "right";
        ctx.fillText(row.value, PANEL_WIDTH - 28, y + rowHeight / 2);
    }

    return canvas.toDataURL("image/png");
}

function panelColors(statusKey, available) {
    if (!available) {
        return {
            background: "#030405",
            row: "rgba(72, 80, 88, 0.10)",
            accent: "rgba(122, 132, 142, 0.12)",
            label: "rgba(142, 152, 162, 0.24)",
            value: "rgba(142, 152, 162, 0.20)",
        };
    }

    if (statusKey === "danger") {
        return {
            background: "#090405",
            row: "#251012",
            accent: "#ff4646",
            label: "#ffc2c2",
            value: "#ff5b5b",
        };
    }

    if (statusKey === "warning") {
        return {
            background: "#080706",
            row: "#241c0b",
            accent: "#ffd166",
            label: "#ffe5a6",
            value: "#ffd166",
        };
    }

    return {
        background: "#040707",
        row: "#0e1716",
        accent: "#5ee6a8",
        label: "#b9ddd0",
        value: "#6ee7b7",
    };
}

function roundRect(ctx, x, y, width, height, radius) {
    ctx.beginPath();
    ctx.moveTo(x + radius, y);
    ctx.lineTo(x + width - radius, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
    ctx.lineTo(x + width, y + height - radius);
    ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
    ctx.lineTo(x + radius, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
    ctx.lineTo(x, y + radius);
    ctx.quadraticCurveTo(x, y, x + radius, y);
    ctx.closePath();
}

function getActionState(snapshot, actionKey) {
    if (snapshot.state.actions && snapshot.state.actions[actionKey]) {
        return snapshot.state.actions[actionKey];
    }

    if (actionKey === "landing-gear") {
        return {
            statusKey: snapshot.state.gearStatus,
            title: snapshot.state.gearTitle,
            isBlinking: snapshot.state.gearBlinking,
            isEnabled: true,
            alertLevel: snapshot.state.gearAlertLevel,
        };
    }

    return null;
}

function startBlink(context) {
    const state = contexts.get(context);
    if (!state) return;
    if (state.blinkId) return; // already blinking

    state.blinkPhaseOn = true;
    state.blinkId = setInterval(function () {
        const s = contexts.get(context);
        if (!s) return;
        s.blinkPhaseOn = !s.blinkPhaseOn;
        const url = s.blinkPhaseOn ? s.onImageUrl : s.offImageUrl;
        if (url) setImage(context, url);
    }, BLINK_INTERVAL_MS);
}

function stopBlink(context) {
    const state = contexts.get(context);
    if (!state) return;
    if (state.blinkId) {
        clearInterval(state.blinkId);
        state.blinkId = null;
    }
    state.blinkPhaseOn = true;
}

function applyFallback(context) {
    const state = contexts.get(context);
    if (!state) return;

    if (state.isPanel) {
        const model = buildPanelModel(null);
        state.lastPanelSignature = JSON.stringify(model);
        setImage(context, renderPanel(model));
        return;
    }

    if (state.lastStatus !== "unavailable") {
        state.lastStatus = "unavailable";
        const assetName = state.statusToAsset.unavailable || state.statusToAsset.unknown;
        loadAsset(assetName).then(function (dataUrl) {
            const current = contexts.get(context);
            if (current === state) {
                current.onImageUrl = dataUrl;
                setImage(context, dataUrl);
            }
        });
    }
    if (state.shouldSetTitle && state.lastTitle !== state.fallbackTitle) {
        state.lastTitle = state.fallbackTitle;
        setTitle(context, state.fallbackTitle);
    }
    if (state.lastBlinking) {
        state.lastBlinking = false;
        stopBlink(context);
    }
}

function setTitle(context, title) {
    if (!websocket || websocket.readyState !== 1) return;
    websocket.send(JSON.stringify({
        event: "setTitle",
        context: context,
        payload: {
            title: title,
            target: 0,
            state: 0,
        },
    }));
}

function loadAsset(name) {
    if (assetCache.has(name)) {
        return Promise.resolve(assetCache.get(name));
    }

    return fetch("../assets/" + name)
        .then(function (response) {
            if (!response.ok) throw new Error("asset " + response.status);
            return response.blob();
        })
        .then(function (blob) {
            return new Promise(function (resolve, reject) {
                const reader = new FileReader();
                reader.onloadend = function () {
                    const dataUrl = reader.result;
                    assetCache.set(name, dataUrl);
                    resolve(dataUrl);
                };
                reader.onerror = reject;
                reader.readAsDataURL(blob);
            });
        })
        .catch(function () {
            return "";
        });
}

function setImage(context, dataUrl) {
    if (!websocket || websocket.readyState !== 1 || !dataUrl) return;
    websocket.send(JSON.stringify({
        event: "setImage",
        context: context,
        payload: {
            image: dataUrl,
            target: 0,
            state: 0,
        },
    }));
}

function triggerAction(actionKey) {
    fetch(API_BASE + "/api/actions/" + encodeURIComponent(actionKey), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{}",
    }).catch(function () {
        // Next poll will refresh state
    });
}

function sendHeartbeat(status) {
    if (heartbeatInFlight) return;
    heartbeatInFlight = true;

    fetch(API_BASE + "/api/stream-controller/status", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status: status }),
    }).catch(function () {
        // Silently ignore
    }).finally(function () {
        heartbeatInFlight = false;
    });
}

// Expose for the StreamDock host
if (typeof window !== "undefined") {
    window.connectElgatoStreamDeckSocket = connectElgatoStreamDeckSocket;
}
