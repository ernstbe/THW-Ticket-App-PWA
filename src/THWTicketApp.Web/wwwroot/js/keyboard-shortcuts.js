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

    // Ignore when typing in inputs
    const tag = e.target.tagName;
    const isEditable = e.target.isContentEditable;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || isEditable) return;

    // Ctrl/Cmd shortcuts
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
