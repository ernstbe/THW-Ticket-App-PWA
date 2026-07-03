// IndexedDB interop for offline caching
const DB_NAME = 'thw-ticket-cache';
const DB_VERSION = 2;
// Cap for the diagnostic syncLog store — it's persistent and appended on every
// drain step, so without eviction it grows forever and eventually eats the
// origin's storage quota, starving the offline queue it's meant to diagnose (#224).
const SYNC_LOG_MAX = 500;

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
    const tickets = JSON.parse(ticketsJson);
    // Resolve only once the transaction COMMITS, and reject on failure — the old
    // code returned true before the writes committed (and split them across two
    // transactions), so a failed cache write (e.g. QuotaExceededError) was
    // silently reported as success (#207). One atomic tx over both stores.
    return new Promise((resolve, reject) => {
        const tx = database.transaction(['tickets', 'meta'], 'readwrite');
        const store = tx.objectStore('tickets');
        for (const ticket of tickets) {
            store.put(ticket);
        }
        tx.objectStore('meta').put({ key: 'lastCacheTime', value: new Date().toISOString() });
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error || new Error('saveTickets transaction aborted'));
    });
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
    // One atomic tx over both stores; await commit and reject on failure (#258).
    return new Promise((resolve, reject) => {
        const tx = database.transaction(['tickets', 'meta'], 'readwrite');
        tx.objectStore('tickets').clear();
        tx.objectStore('meta').delete('lastCacheTime');
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error || new Error('clearTicketCache transaction aborted'));
    });
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
    // Await COMMIT. A lost removal is worse than a lost write: the C# side logs
    // "Synced successfully" and, on the next drain, re-reads and RE-APPLIES the
    // already-synced action (duplicate comment/status/upload), or strands it as a
    // bogus self-conflict. Reject on error/abort so it surfaces (#253).
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        tx.objectStore('pendingActions').delete(id);
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error || new Error('removePendingAction transaction aborted'));
    });
}

export async function clearPendingActions() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        tx.objectStore('pendingActions').clear();
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error || new Error('clearPendingActions transaction aborted'));
    });
}

export async function markActionConflicted(id, reason, conflictType) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction('pendingActions', 'readwrite');
        const store = tx.objectStore('pendingActions');
        const request = store.get(id);
        let found = false;
        request.onsuccess = () => {
            const action = request.result;
            if (!action) return;
            found = true;
            action.isConflicted = true;
            action.conflictReason = reason;
            action.conflictType = conflictType || 'TicketUpdated';
            store.put(action);
        };
        request.onerror = () => reject(request.error);
        // Resolve only once the put COMMITS; reject on failure (#258).
        tx.oncomplete = () => resolve(found);
        tx.onabort = () => reject(tx.error || new Error('markActionConflicted transaction aborted'));
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
        let found = false;
        request.onsuccess = () => {
            const action = request.result;
            if (!action) return;
            found = true;
            action.retryCount = retryCount;
            action.nextRetryAt = nextRetryAtIso;
            action.lastErrorMessage = errorMessage || null;
            store.put(action);
        };
        request.onerror = () => reject(request.error);
        // Resolve only once the put COMMITS — otherwise a failed put was reported
        // as success, so retryCount/nextRetryAt never persisted and the action
        // busy-retried with no backoff and was never aged out (#258).
        tx.oncomplete = () => resolve(found);
        tx.onabort = () => reject(tx.error || new Error('updateRetryState transaction aborted'));
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
        let found = false;
        request.onsuccess = () => {
            const action = request.result;
            if (!action) return;
            found = true;
            action.ticketUpdatedAt = ticketUpdatedAtIso;
            store.put(action);
        };
        request.onerror = () => reject(request.error);
        // Resolve only once the put COMMITS; reject on failure (#258).
        tx.oncomplete = () => resolve(found);
        tx.onabort = () => reject(tx.error || new Error('updateActionBaseline transaction aborted'));
    });
}

export async function appendSyncLog(entryJson) {
    const database = await openDb();
    const entry = JSON.parse(entryJson);
    // Await commit and surface failures rather than returning true early (#207).
    return new Promise((resolve, reject) => {
        const tx = database.transaction('syncLog', 'readwrite');
        const store = tx.objectStore('syncLog');
        store.add(entry);
        // Evict the oldest rows beyond the cap in the same transaction. openCursor
        // iterates ascending by the autoIncrement key, i.e. oldest first (#224).
        const countReq = store.count();
        countReq.onsuccess = () => {
            let toDelete = countReq.result - SYNC_LOG_MAX;
            if (toDelete <= 0) return;
            store.openCursor().onsuccess = (e) => {
                const cursor = e.target.result;
                if (cursor && toDelete > 0) {
                    cursor.delete();
                    toDelete--;
                    cursor.continue();
                }
            };
        };
        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error || new Error('appendSyncLog transaction aborted'));
    });
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
