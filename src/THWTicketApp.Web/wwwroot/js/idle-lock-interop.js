// Idle-watcher that locks the app after a configurable timeout of no
// user activity. Calls back into Blazor via DotNetObjectReference; the
// C# side then decides whether to actually lock (only does so if a
// passkey is registered, otherwise the user has no way back in).

let idleTimer = null;
let dotNetRef = null;
let timeoutMs = 0;
let listenersAttached = false;

// Note: do NOT include 'visibilitychange' here. It fires both when the
// tab becomes hidden AND when it becomes visible again, which means the
// timer would be reset every time the user comes back to a long-idle
// tab — defeating the entire purpose. We want the timeout to elapse
// while the tab is in the background.
const ACTIVITY_EVENTS = [
    'mousemove',
    'mousedown',
    'keydown',
    'touchstart',
    'scroll',
    'click',
    'wheel'
];

function reset() {
    if (idleTimer) {
        clearTimeout(idleTimer);
        idleTimer = null;
    }
    if (timeoutMs > 0 && dotNetRef) {
        idleTimer = setTimeout(fire, timeoutMs);
    }
}

function fire() {
    idleTimer = null;
    if (dotNetRef) {
        try {
            dotNetRef.invokeMethodAsync('OnIdle');
        } catch (e) {
            // ref already disposed — nothing to do
        }
    }
}

function attachListeners() {
    if (listenersAttached) return;
    for (const evt of ACTIVITY_EVENTS) {
        window.addEventListener(evt, reset, { passive: true });
    }
    listenersAttached = true;
}

export function startIdleWatch(ref, minutes) {
    dotNetRef = ref;
    timeoutMs = Math.max(0, minutes) * 60 * 1000;
    attachListeners();
    reset();
}

export function updateTimeout(minutes) {
    timeoutMs = Math.max(0, minutes) * 60 * 1000;
    reset();
}

export function stopIdleWatch() {
    if (idleTimer) {
        clearTimeout(idleTimer);
        idleTimer = null;
    }
    dotNetRef = null;
    timeoutMs = 0;
    // Leave listeners attached — they no-op when dotNetRef is null and
    // it's cheaper than re-attaching on every login.
}
