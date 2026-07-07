// Browser Web Notifications interop for Blazor

export function isSupported() {
    return 'Notification' in window;
}

export function getPermission() {
    if (!('Notification' in window)) return 'denied';
    return Notification.permission;
}

export async function requestPermission() {
    if (!('Notification' in window)) return 'denied';
    return await Notification.requestPermission();
}

export async function showNotification(title, body, tag, url) {
    if (!('Notification' in window) || Notification.permission !== 'granted') return false;

    // Prefer Service Worker notifications (work in background tabs).
    // navigator.serviceWorker.ready NEVER resolves (per spec) until a registration
    // with an *active* worker exists. Where the SW API is present but no active
    // worker is (private windows, enterprise policy, a failed SW script fetch,
    // some Safari configs), awaiting `.ready` hangs forever and the notification
    // never fires. getRegistration() resolves to undefined instead, so we fall
    // back cleanly to the page-level notification below (#312, mirrors #216).
    if ('serviceWorker' in navigator) {
        try {
            const reg = await navigator.serviceWorker.getRegistration();
            if (reg && reg.active) {
                await reg.showNotification(title, {
                    body: body,
                    tag: tag,
                    icon: '/app/icon-192.png',
                    badge: '/app/icon-192.png',
                    data: { url: url },
                    requireInteraction: false
                });
                return true;
            }
            // No active worker — fall through to the page-level fallback.
        } catch { /* fall through to legacy */ }
    }

    // Fallback: page-level notification (only works when tab is active)
    const notification = new Notification(title, {
        body: body,
        tag: tag,
        icon: '/app/icon-192.png',
        badge: '/app/icon-192.png',
        requireInteraction: false
    });

    if (url) {
        notification.onclick = () => {
            window.focus();
            window.location.href = url;
            notification.close();
        };
    }

    setTimeout(() => notification.close(), 8000);
    return true;
}
