// PWA helper functions exposed as globals on `window.pwaHelpers`.
// The Caddy CSP for /app/* does not include 'unsafe-eval' — script-src
// is 'self' + 'wasm-unsafe-eval' + 'unsafe-inline' + cdn.socket.io. That
// means JS.InvokeAsync(..., "eval", "<string>") calls from C# get blocked
// and trigger the gelbe Blazor-Error-UI. Anything we previously did via
// eval() lives here as a named function and is reached via
// JsRuntime.InvokeAsync<T>("pwaHelpers.functionName", args).
//
// Keep this file dependency-free: it must work before Blazor boots and
// without any module imports.
(function () {
    window.pwaHelpers = {
        // Pre-loaded dark mode (read sync from index.html before Blazor boots).
        // Returns "true" | "false" | null (null = follow OS).
        getPreloadedDarkMode: function () { return window.__preloadedDarkMode || null; },

        // Connectivity / environment ---------------------------------------
        isOnline: function () { return navigator.onLine; },

        prefersDarkMode: function () {
            try { return window.matchMedia('(prefers-color-scheme: dark)').matches; }
            catch { return false; }
        },

        getUserAgent: function () { return navigator.userAgent; },
        getLanguage: function () { return navigator.language; },
        getReferrer: function () { return document.referrer; },
        getViewport: function () { return window.innerWidth + 'x' + window.innerHeight; },

        // Bug-Report context ------------------------------------------------
        getConsoleTail: function () {
            try { return JSON.stringify(window.__consoleTail || []); }
            catch { return '[]'; }
        },

        // DOM helpers -------------------------------------------------------
        scrollToElement: function (id, options) {
            var el = document.getElementById(id);
            if (el) el.scrollIntoView(options || { behavior: 'smooth', block: 'start' });
        },

        // File download via data-URL. mime e.g. 'text/csv', 'application/json'.
        // base64 is the file body, already base64-encoded. filename is the
        // download attribute.
        downloadBase64: function (mime, base64, filename) {
            var a = document.createElement('a');
            a.href = 'data:' + mime + ';base64,' + base64;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
        },

        // Service worker update check. Returns true if a new bundle is
        // installing or waiting. The controllerchange listener in index.html
        // handles the reload; we just nudge the SW.
        checkForUpdate: async function () {
            if (!('serviceWorker' in navigator)) return false;
            var reg = await navigator.serviceWorker.getRegistration();
            if (!reg) return false;
            await reg.update();
            // Give install a moment to settle before we look at .waiting/.installing.
            await new Promise(function (r) { setTimeout(r, 1500); });
            var target = reg.waiting || reg.installing;
            if (target) {
                if (reg.waiting) reg.waiting.postMessage('SKIP_WAITING');
                return true;
            }
            return false;
        }
    };
})();
