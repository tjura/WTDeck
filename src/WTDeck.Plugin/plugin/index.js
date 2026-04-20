/*
 * WTDeck StreamDock plugin - SDK v1
 *
 * Thin transport layer:
 *   - polls http://127.0.0.1:8730/api/stream-dock/state every 500ms
 *   - POSTs /api/actions/landing-gear on button press
 *   - PUTs /api/stream-controller/status heartbeat every 2s
 *   - blinks the button every 500ms when gearBlinking is true
 *
 * All business logic lives in WTDeck.App (.NET). This plugin only renders state.
 */

"use strict";

const API_BASE = "http://127.0.0.1:8730";
const POLL_INTERVAL_MS = 500;
const HEARTBEAT_INTERVAL_MS = 2000;
const BLINK_INTERVAL_MS = 500;
const GEAR_ACTION_UUID = "com.wtdeck.streamdock.gear";
const GEAR_ACTION_KEY = "landing-gear";
const BLINK_OFF_ASSET = "gear-blink-off.svg";

// Map status keys from the backend to local asset filenames
const STATUS_TO_ASSET = {
    up: "gear-retracted.svg",
    down: "gear-deployed.svg",
    extending: "gear-deploying.svg",
    retracting: "gear-retracting.svg",
    danger: "gear-damaged.svg",
    unavailable: "gear-disabled.svg",
    unknown: "gear-unknown.svg",
};

let websocket = null;
let pluginUUID = null;
const contexts = new Map(); // context -> per-context state
const assetCache = new Map(); // assetName -> dataUrl
let pollId = null;
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

    if (event === "willAppear" && action === GEAR_ACTION_UUID) {
        startContext(context);
    } else if (event === "willDisappear" && action === GEAR_ACTION_UUID) {
        stopContext(context);
    } else if (event === "keyDown" && action === GEAR_ACTION_UUID) {
        triggerAction(GEAR_ACTION_KEY);
    }
}

function createContextState() {
    return {
        blinkId: null,
        blinkPhaseOn: true,
        lastStatus: null,
        lastBlinking: false,
        onImageUrl: null,  // current state's full-color image
        offImageUrl: null, // blink-off image (text only)
    };
}

function startContext(context) {
    if (contexts.has(context)) return;

    const state = createContextState();
    contexts.set(context, state);

    // Preload the blink-off asset (reused across all blinking states)
    loadAsset(BLINK_OFF_ASSET).then(function (dataUrl) {
        const s = contexts.get(context);
        if (s === state) s.offImageUrl = dataUrl;
    });

    if (lastSnapshot && lastSnapshot.state) {
        applyState(context, lastSnapshot);
    } else {
        syncAllContexts();
    }

    ensureTransportLoopsRunning();
}

function stopContext(context) {
    const state = contexts.get(context);
    if (!state) return;

    if (state.blinkId) clearInterval(state.blinkId);
    contexts.delete(context);

    if (contexts.size === 0) {
        stopTransportLoops();
        sendHeartbeat("disconnected");
    }
}

function stopAllContexts() {
    contexts.forEach(function (state) {
        if (state.blinkId) clearInterval(state.blinkId);
    });
    contexts.clear();
    stopTransportLoops();
}

function ensureTransportLoopsRunning() {
    if (!pollId) {
        pollId = setInterval(function () {
            syncAllContexts();
        }, POLL_INTERVAL_MS);
    }

    if (!heartbeatId) {
        sendHeartbeat("connected");
        heartbeatId = setInterval(function () {
            sendHeartbeat("connected");
        }, HEARTBEAT_INTERVAL_MS);
    }
}

function stopTransportLoops() {
    if (pollId) {
        clearInterval(pollId);
        pollId = null;
    }

    if (heartbeatId) {
        clearInterval(heartbeatId);
        heartbeatId = null;
    }
}

function syncAllContexts() {
    if (pollInFlight || contexts.size === 0) return;

    pollInFlight = true;

    fetch(API_BASE + "/api/stream-dock/state", { cache: "no-store" })
        .then(function (response) {
            if (!response.ok) throw new Error("http " + response.status);
            return response.json();
        })
        .then(function (snapshot) {
            lastSnapshot = snapshot;
            contexts.forEach(function (_, context) {
                applyState(context, snapshot);
            });
        })
        .catch(function () {
            lastSnapshot = null;
            contexts.forEach(function (_, context) {
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

    const status = snapshot.state.gearStatus || "unknown";
    const blinking = !!snapshot.state.gearBlinking;
    const statusChanged = status !== state.lastStatus;
    const blinkingChanged = blinking !== state.lastBlinking;

    if (statusChanged) {
        state.lastStatus = status;
        const assetName = STATUS_TO_ASSET[status] || STATUS_TO_ASSET.unknown;
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

    if (state.lastStatus !== "unavailable") {
        state.lastStatus = "unavailable";
        loadAsset(STATUS_TO_ASSET.unavailable).then(function (dataUrl) {
            const current = contexts.get(context);
            if (current === state) {
                current.onImageUrl = dataUrl;
                setImage(context, dataUrl);
            }
        });
    }
    if (state.lastBlinking) {
        state.lastBlinking = false;
        stopBlink(context);
    }
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
