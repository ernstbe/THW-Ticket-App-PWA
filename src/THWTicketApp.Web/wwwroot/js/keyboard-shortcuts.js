// Global keyboard shortcuts interop
let _shortcutRef = null;

export function register(dotNetRef) {
    _shortcutRef = dotNetRef;
    document.addEventListener('keydown', handleKeyDown);
}

export function unregister() {
    document.removeEventListener('keydown', handleKeyDown);
    _shortcutRef = null;
}

function handleKeyDown(e) {
    if (!_shortcutRef) return;

    // Ctrl+K / Cmd+K: command palette. Special-cased BEFORE the
    // input-ignore check so the user can summon the palette from any
    // focused field (matches Linear / Notion / GitHub behaviour).
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
        e.preventDefault();
        _shortcutRef.invokeMethodAsync('OnShortcut', 'palette');
        return;
    }

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
