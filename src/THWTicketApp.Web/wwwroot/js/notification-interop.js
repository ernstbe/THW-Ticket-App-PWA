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

    // Prefer Service Worker notifications (work in background tabs)
    if ('serviceWorker' in navigator) {
        try {
            const reg = await navigator.serviceWorker.ready;
            await reg.showNotification(title, {
                body: body,
                tag: tag,
                icon: '/app/icon-192.png',
                badge: '/app/icon-192.png',
                data: { url: url },
                requireInteraction: false
            });
            return true;
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
