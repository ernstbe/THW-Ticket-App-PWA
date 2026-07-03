// IndexedDB interop for offline caching
const DB_NAME = 'thw-ticket-cache';
const DB_VERSION = 2;

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
            if (!database.objectStoreNames.contains('syncLog'))
                database.createObjectStore('syncLog', { keyPath: 'id', autoIncrement: true });
        };
        request.onsuccess = (e) => {
            db = e.target.result;
            // If another tab later opens a higher DB_VERSION, its upgrade would be
            // blocked as long as we hold this connection open. Close and drop it so
            // the other tab can upgrade; the next openDb() call here re-opens.
            db.onversionchange = () => { db.close(); db = null; };
            resolve(db);
        };
        request.onerror = (e) => reject(e.target.error);
        // Another connection (a second tab, or the previous SPA instance during a
        // soft reload) still holding an older DB_VERSION blocks the upgrade: the
        // browser fires 'blocked' and NEITHER 'success' NOR 'error'. Without this
        // handler the Promise never settles and every awaiting C# call hangs
        // forever. Reject so the deadlock surfaces as an error instead.
        request.onblocked = () => reject(new Error(
            'IndexedDB upgrade blocked by another open connection (close other tabs of this app and retry).'));
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
    const action = JSON.parse(actionJson);
    // Resolve only once the transaction COMMITS. Returning true right after add()
    // reported success even when the write later failed (quota exceeded,
    // ConstraintError, aborted tx) — the offline mutation was silently lost and
    // the UI showed it as accepted. Wait for oncomplete and reject on error/abort
    // so EnqueueAsync can surface the failure to the user.
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const request = tx.objectStore('pendingActions').add(action);
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error || request.error);
        tx.onabort = () => reject(tx.error || request.error
            || new Error('enqueuePendingAction transaction aborted'));
    });
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

export async function markActionConflicted(id, reason, conflictType) {
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
                action.conflictType = conflictType || 'TicketUpdated';
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

export async function updateRetryState(id, nextRetryAtIso, retryCount, errorMessage) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const store = tx.objectStore('pendingActions');
        const request = store.get(id);
        request.onsuccess = () => {
            const action = request.result;
            if (!action) { resolve(false); return; }
            action.retryCount = retryCount;
            action.nextRetryAt = nextRetryAtIso;
            action.lastErrorMessage = errorMessage || null;
            store.put(action);
            resolve(true);
        };
        request.onerror = () => reject(request.error);
    });
}

// Advance a queued action's captured baseline (TicketUpdatedAt). Used after an
// earlier same-ticket action in the same drain applied and bumped the server's
// 'updated' value, so this action isn't flagged as a self-inflicted conflict.
export async function updateActionBaseline(id, ticketUpdatedAtIso) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const store = tx.objectStore('pendingActions');
        const request = store.get(id);
        request.onsuccess = () => {
            const action = request.result;
            if (!action) { resolve(false); return; }
            action.ticketUpdatedAt = ticketUpdatedAtIso;
            store.put(action);
            resolve(true);
        };
        request.onerror = () => reject(request.error);
    });
}

export async function appendSyncLog(entryJson) {
    const database = await openDb();
    const tx = database.transaction('syncLog', 'readwrite');
    const entry = JSON.parse(entryJson);
    tx.objectStore('syncLog').add(entry);
    return true;
}

export async function getSyncLog(limit) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('syncLog', 'readonly');
        const request = tx.objectStore('syncLog').getAll();
        request.onsuccess = () => {
            const all = request.result;
            // Return newest first, capped at `limit` if provided
            all.sort((a, b) => (b.id || 0) - (a.id || 0));
            const sliced = limit > 0 ? all.slice(0, limit) : all;
            resolve(JSON.stringify(sliced));
        };
        request.onerror = () => reject(request.error);
    });
}

export async function clearSyncLog() {
    const database = await openDb();
    const tx = database.transaction('syncLog', 'readwrite');
    tx.objectStore('syncLog').clear();
    return true;
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
