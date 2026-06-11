// Drag-and-drop attachment hookup.
//
// We can't pass File objects directly across the JS-Blazor boundary as
// a Microsoft.AspNetCore.Components.Forms.InputFile would. So instead
// this module attaches dragover/drop listeners to an element, takes
// the dropped file(s), drops them onto a sibling <input type="file">
// (the InputFile that already handles upload), and triggers its change
// event. Blazor's InputFile then sees a file selection and routes it
// through the existing OnFileSelectedAsync handler — no new server-side
// code path needed.

const handlers = new Map(); // dropzoneId -> { onOver, onLeave, onDrop, target }

function findFileInput(zoneEl, inputId) {
    if (!inputId) return null;
    // First try by id (most reliable); fall back to scanning the zone.
    return document.getElementById(inputId) ||
           zoneEl.querySelector(`input[type="file"]`);
}

// Returns true when the zone element existed and listeners were bound —
// callers that render the zone only after async data arrives (skeleton
// phase) retry on later renders until this reports success.
export function register(zoneId, inputId) {
    const zone = document.getElementById(zoneId);
    if (!zone) return false;

    // Defensive: don't double-bind if Blazor re-renders the page.
    if (handlers.has(zoneId)) unregister(zoneId);

    const onOver = (e) => {
        e.preventDefault();
        zone.classList.add('pwa-dropzone-active');
    };
    const onLeave = (e) => {
        // Only clear when the drag genuinely leaves the zone, not when it
        // crosses over a child element.
        if (e.target === zone || !zone.contains(e.relatedTarget)) {
            zone.classList.remove('pwa-dropzone-active');
        }
    };
    const onDrop = (e) => {
        e.preventDefault();
        zone.classList.remove('pwa-dropzone-active');
        const input = findFileInput(zone, inputId);
        if (!input || !e.dataTransfer || !e.dataTransfer.files?.length) return;

        // Forward the dropped files into the InputFile and trigger change.
        input.files = e.dataTransfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    zone.addEventListener('dragover', onOver);
    zone.addEventListener('dragleave', onLeave);
    zone.addEventListener('drop', onDrop);
    handlers.set(zoneId, { onOver, onLeave, onDrop, zone });
    return true;
}

export function unregister(zoneId) {
    const entry = handlers.get(zoneId);
    if (!entry) return;
    entry.zone.removeEventListener('dragover', entry.onOver);
    entry.zone.removeEventListener('dragleave', entry.onLeave);
    entry.zone.removeEventListener('drop', entry.onDrop);
    handlers.delete(zoneId);
}
