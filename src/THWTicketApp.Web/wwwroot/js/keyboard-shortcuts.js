// Global keyboard shortcuts interop.
//
// Ctrl+K (command palette) is handled in two places:
//
//   1. paletteCapture — attached at WINDOW level with capture:true. This
//      catches the event BEFORE any other listener has a chance to see
//      it AND before the browser's default action (which on most Chromium
//      builds is "focus omnibox / Google search"). It's installed when
//      the module is imported, so it works even before Blazor's
//      register() has been called.
//
//   2. handleKeyDown — the bubble-phase listener, attached on register().
//      It picks up the other single-letter shortcuts (N, T, K, /, ?, etc).
//
// The capture-phase preventDefault is critical: without it Chrome opens
// the address bar in search mode and the keydown never propagates back
// to the page-level handler.

let _shortcutRef = null;
let _captureInstalled = false;

function paletteCapture(e) {
    if (!(e.ctrlKey || e.metaKey)) return;
    if (e.key !== 'k' && e.key !== 'K') return;

    // Always stop the browser default — even if Blazor isn't ready yet,
    // we'd rather have the shortcut do nothing than open Google search.
    e.preventDefault();
    e.stopPropagation();

    if (_shortcutRef) {
        _shortcutRef.invokeMethodAsync('OnShortcut', 'palette');
    } else {
        // The user hit Ctrl+K before Blazor finished booting. Log so we
        // can diagnose if it happens consistently — usually the next
        // press a few ms later will work.
        console.warn('[shortcuts] Ctrl+K pressed before Blazor was ready');
    }
}

// Install the capture-phase listener immediately on module load.
// register() can still come later to attach the bubble-phase one.
if (!_captureInstalled && typeof window !== 'undefined') {
    window.addEventListener('keydown', paletteCapture, { capture: true });
    _captureInstalled = true;
}

export function register(dotNetRef) {
    _shortcutRef = dotNetRef;
    document.addEventListener('keydown', handleKeyDown);
}

export function unregister() {
    document.removeEventListener('keydown', handleKeyDown);
    // Keep paletteCapture installed — it's harmless and avoids a window
    // of "Ctrl+K falls through to Google" during navigation.
    _shortcutRef = null;
}

function handleKeyDown(e) {
    if (!_shortcutRef) return;

    // Ctrl+K is already handled by paletteCapture above. Skip it here so
    // the input-focus check below doesn't suppress a future shortcut.
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) return;

    // Ignore other shortcuts when typing in inputs.
    const tag = e.target.tagName;
    const isEditable = e.target.isContentEditable;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || isEditable) return;

    // Other modified-key combos: let the browser handle them.
    if (e.ctrlKey || e.metaKey) return;

    let action = null;
    switch (e.key) {
        case 'n': action = 'new-ticket'; break;
        case 'k': action = 'kanban'; break;
        case 't': action = 'tickets'; break;
        case 'd': action = 'dashboard'; break;
        case 's': action = 'settings'; break;
        case 'r': action = 'reports'; break;
        case '/': action = 'search'; e.preventDefault(); break;
        case '?': action = 'help'; break;
    }

    if (action) {
        _shortcutRef.invokeMethodAsync('OnShortcut', action);
    }
}
