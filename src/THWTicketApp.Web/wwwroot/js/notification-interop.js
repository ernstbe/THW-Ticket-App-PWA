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

export function showNotification(title, body, tag, url) {
    if (!('Notification' in window) || Notification.permission !== 'granted') return false;

    const notification = new Notification(title, {
        body: body,
        tag: tag,
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        requireInteraction: false
    });

    if (url) {
        notification.onclick = () => {
            window.focus();
            window.location.href = url;
            notification.close();
        };
    }

    // Auto-close after 8 seconds
    setTimeout(() => notification.close(), 8000);
    return true;
}
