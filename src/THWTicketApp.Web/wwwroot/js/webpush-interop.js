// Web Push subscription helpers used by WebPushService on the C# side.
//
// The service worker handles the actual `push` event (see service-worker.published.js).
// These helpers only do the subscription dance:
//   - check Notification permission state
//   - subscribe / unsubscribe via the PushManager
//   - read the current subscription so the C# side can sync server state

export function isSupported() {
    return ('serviceWorker' in navigator) && ('PushManager' in window) && ('Notification' in window);
}

export function getPermission() {
    if (!('Notification' in window)) return 'unsupported';
    return Notification.permission;
}

export async function requestPermission() {
    if (!('Notification' in window)) return 'unsupported';
    return await Notification.requestPermission();
}

// VAPID server key is base64url-encoded — `pushManager.subscribe` wants a Uint8Array.
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(base64);
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; ++i) out[i] = raw.charCodeAt(i);
    return out;
}

// navigator.serviceWorker.ready NEVER resolves (per spec) until a registration
// with an *active* worker exists. In contexts where the SW API is present but no
// active worker is (Firefox private windows, enterprise policy, a failed SW
// script fetch, some Safari configs), awaiting `.ready` hangs forever — which
// stalls logout (DisableAsync is a logout hook), Settings render and onboarding
// (#216). getRegistration() resolves to undefined instead, so we can bail
// cleanly. Returns an active registration or null.
async function getActiveRegistration() {
    if (!isSupported()) return null;
    const reg = await navigator.serviceWorker.getRegistration();
    return reg && reg.active ? reg : null;
}

// Returns { endpoint, keys: { p256dh, auth } } — exactly the shape the
// backend POST /api/v1/account/push/subscribe expects.
export async function subscribe(vapidPublicKey) {
    if (!isSupported()) throw new Error('Push not supported');
    const reg = await getActiveRegistration();
    if (!reg) throw new Error('No active service worker');
    let sub = await reg.pushManager.getSubscription();
    if (!sub) {
        sub = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
        });
    }
    return subscriptionToJson(sub);
}

export async function unsubscribe() {
    const reg = await getActiveRegistration();
    if (!reg) return null;
    const sub = await reg.pushManager.getSubscription();
    if (!sub) return null;
    const endpoint = sub.endpoint;
    await sub.unsubscribe();
    return endpoint;
}

export async function getCurrentSubscription() {
    const reg = await getActiveRegistration();
    if (!reg) return null;
    const sub = await reg.pushManager.getSubscription();
    return sub ? subscriptionToJson(sub) : null;
}

function subscriptionToJson(sub) {
    const raw = sub.toJSON();
    return {
        endpoint: raw.endpoint,
        keys: {
            p256dh: raw.keys && raw.keys.p256dh,
            auth: raw.keys && raw.keys.auth
        }
    };
}
