// Lightweight pull-to-refresh for the PWA. Triggers a Blazor callback
// when the user pulls down from the top of the page beyond a threshold,
// while the document is already scrolled to top.
//
// Behaviour borrowed from typical native apps: small visual indicator
// follows the finger, snaps back if released early, fires the refresh
// callback if released past the threshold.

const THRESHOLD = 80;          // px the user must pull
const RESISTANCE = 2.5;        // higher = harder to pull (more "stretchy")
const MAX_OFFSET = 140;        // px clamp so the indicator doesn't fly off-screen

let dotNetRef = null;
let startY = null;
let currentY = 0;
let active = false;
let indicator = null;

function ensureIndicator() {
    if (indicator) return indicator;
    indicator = document.createElement('div');
    indicator.id = 'pwa-ptr-indicator';
    indicator.style.cssText = [
        'position:fixed', 'top:0', 'left:50%', 'transform:translate(-50%,-100%)',
        'background:#003399', 'color:#fff', 'padding:8px 16px',
        'border-radius:0 0 12px 12px', 'font-family:Roboto,sans-serif',
        'font-size:14px', 'z-index:10001',
        'transition:transform 0.2s ease-out, opacity 0.2s ease-out',
        'opacity:0', 'pointer-events:none',
        'box-shadow:0 4px 12px rgba(0,0,0,0.2)'
    ].join(';');
    indicator.textContent = '↓ Zum Aktualisieren ziehen';
    document.body.appendChild(indicator);
    return indicator;
}

function setIndicator(offset, ready) {
    const el = ensureIndicator();
    if (offset <= 0) {
        el.style.transform = 'translate(-50%,-100%)';
        el.style.opacity = '0';
        return;
    }
    const clamped = Math.min(offset, MAX_OFFSET);
    el.style.transform = `translate(-50%, ${clamped - 50}px)`;
    el.style.opacity = String(Math.min(clamped / THRESHOLD, 1));
    el.textContent = ready ? '↑ Loslassen zum Aktualisieren' : '↓ Zum Aktualisieren ziehen';
}

function onTouchStart(e) {
    if (window.scrollY > 0) return;
    if (e.touches.length !== 1) return;
    startY = e.touches[0].clientY;
    currentY = startY;
    active = true;
}

function onTouchMove(e) {
    if (!active || startY === null) return;
    currentY = e.touches[0].clientY;
    const raw = currentY - startY;
    if (raw <= 0) {
        setIndicator(0, false);
        return;
    }
    const offset = raw / RESISTANCE;
    setIndicator(offset, offset >= THRESHOLD);
}

function onTouchEnd() {
    if (!active) return;
    const raw = currentY - (startY ?? currentY);
    const offset = raw / RESISTANCE;
    active = false;
    startY = null;

    if (offset >= THRESHOLD) {
        // Show "loading" state while the callback runs.
        const el = ensureIndicator();
        el.textContent = '⟳ Aktualisiere...';
        el.style.transform = 'translate(-50%, 30px)';
        el.style.opacity = '1';

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnPullRefresh').finally(() => {
                setIndicator(0, false);
            });
        } else {
            setIndicator(0, false);
        }
    } else {
        setIndicator(0, false);
    }
}

export function register(ref) {
    dotNetRef = ref;
    document.addEventListener('touchstart', onTouchStart, { passive: true });
    document.addEventListener('touchmove', onTouchMove, { passive: true });
    document.addEventListener('touchend', onTouchEnd, { passive: true });
    document.addEventListener('touchcancel', onTouchEnd, { passive: true });
}

export function unregister() {
    dotNetRef = null;
    document.removeEventListener('touchstart', onTouchStart);
    document.removeEventListener('touchmove', onTouchMove);
    document.removeEventListener('touchend', onTouchEnd);
    document.removeEventListener('touchcancel', onTouchEnd);
}
