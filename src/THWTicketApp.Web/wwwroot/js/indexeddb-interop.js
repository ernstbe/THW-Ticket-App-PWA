// IndexedDB interop for offline caching
const DB_NAME = 'thw-ticket-cache';
const DB_VERSION = 1;

let db = null;

function openDb() {
    return new Promise((resolve, reject) => {
        if (db) { resolve(db); return; }
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = (e) => {
            const database = e.target.result;
            if (!database.objectStoreNames.contains('tickets'))
                database.createObjectStore('tickets', { keyPath: '_id' });
            if (!database.objectStoreNames.contains('pendingActions'))
                database.createObjectStore('pendingActions', { keyPath: 'id', autoIncrement: true });
            if (!database.objectStoreNames.contains('meta'))
                database.createObjectStore('meta', { keyPath: 'key' });
        };
        request.onsuccess = (e) => { db = e.target.result; resolve(db); };
        request.onerror = (e) => reject(e.target.error);
    });
}

export async function saveTickets(ticketsJson) {
    const database = await openDb();
    const tx = database.transaction('tickets', 'readwrite');
    const store = tx.objectStore('tickets');
    const tickets = JSON.parse(ticketsJson);
    for (const ticket of tickets) {
        store.put(ticket);
    }
    // Save cache timestamp
    const metaTx = database.transaction('meta', 'readwrite');
    metaTx.objectStore('meta').put({ key: 'lastCacheTime', value: new Date().toISOString() });
    return true;
}

export async function getTickets() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('tickets', 'readonly');
        const request = tx.objectStore('tickets').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function getCachedTicketCount() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('tickets', 'readonly');
        const request = tx.objectStore('tickets').count();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function clearTicketCache() {
    const database = await openDb();
    const tx = database.transaction('tickets', 'readwrite');
    tx.objectStore('tickets').clear();
    const metaTx = database.transaction('meta', 'readwrite');
    metaTx.objectStore('meta').delete('lastCacheTime');
    return true;
}

export async function enqueuePendingAction(actionJson) {
    const database = await openDb();
    const tx = database.transaction('pendingActions', 'readwrite');
    const action = JSON.parse(actionJson);
    tx.objectStore('pendingActions').add(action);
    return true;
}

export async function getPendingActions() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readonly');
        const request = tx.objectStore('pendingActions').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function getPendingActionCount() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readonly');
        const request = tx.objectStore('pendingActions').count();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function removePendingAction(id) {
    const database = await openDb();
    const tx = database.transaction('pendingActions', 'readwrite');
    tx.objectStore('pendingActions').delete(id);
    return true;
}

export async function clearPendingActions() {
    const database = await openDb();
    const tx = database.transaction('pendingActions', 'readwrite');
    tx.objectStore('pendingActions').clear();
    return true;
}

export async function markActionConflicted(id, reason) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const store = tx.objectStore('pendingActions');
        const request = store.get(id);
        request.onsuccess = () => {
            const action = request.result;
            if (action) {
                action.isConflicted = true;
                action.conflictReason = reason;
                store.put(action);
                resolve(true);
            } else {
                resolve(false);
            }
        };
        request.onerror = () => reject(request.error);
    });
}

export async function getConflictedActions() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readonly');
        const request = tx.objectStore('pendingActions').getAll();
        request.onsuccess = () => {
            const conflicted = request.result.filter(a => a.isConflicted === true);
            resolve(JSON.stringify(conflicted));
        };
        request.onerror = () => reject(request.error);
    });
}

export async function incrementRetryCount(id) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const store = tx.objectStore('pendingActions');
        const request = store.get(id);
        request.onsuccess = () => {
            const action = request.result;
            if (action) {
                action.retryCount = (action.retryCount || 0) + 1;
                store.put(action);
                resolve(action.retryCount);
            } else {
                resolve(-1);
            }
        };
        request.onerror = () => reject(request.error);
    });
}

export async function getLastCacheTime() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('meta', 'readonly');
        const request = tx.objectStore('meta').get('lastCacheTime');
        request.onsuccess = () => resolve(request.result ? request.result.value : null);
        request.onerror = () => reject(request.error);
    });
}
