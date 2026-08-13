// Serializes the offline sync drain across browser tabs sharing this
// origin, using the Web Locks API. Without this, two tabs handling the
// same 'online' event both read the same IndexedDB pending-actions
// snapshot and apply non-idempotent actions (AddComment, CreateTicket,
// attachment upload, ...) twice.
//
// dotNetRef must expose a JSInvokable `DrainQueueAsync` method returning
// bool. Browsers without navigator.locks (older Safari) fall back to
// running the callback directly — best effort, not a regression from the
// unlocked behavior this replaces.
export async function runExclusive(dotNetRef) {
    if (!('locks' in navigator)) {
        return await dotNetRef.invokeMethodAsync('DrainQueueAsync');
    }

    return await navigator.locks.request('thw-sync-drain', async () => {
        return await dotNetRef.invokeMethodAsync('DrainQueueAsync');
    });
}
