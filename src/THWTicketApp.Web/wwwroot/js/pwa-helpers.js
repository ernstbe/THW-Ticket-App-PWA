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

        // Download a URL under a caller-chosen filename. Attachments are stored on
        // disk under a random hash (attachment_<hash>.pdf), so a plain navigation
        // saves the file under that hash — the user's original name is only in the
        // ticket data (attachment.name). Fetch as a blob and click an
        // <a download="..."> so the saved file keeps the original name (#254/ISSUE-6).
        // Returns false so the C# caller can fall back to a plain navigation.
        downloadUrl: async function (url, filename) {
            try {
                var resp = await fetch(url, { credentials: 'same-origin' });
                if (!resp.ok) return false;
                var blob = await resp.blob();
                var objectUrl = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = objectUrl;
                a.download = filename || '';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                setTimeout(function () { URL.revokeObjectURL(objectUrl); }, 10000);
                return true;
            } catch (e) {
                return false;
            }
        },

        // Wrap the current selection in a textarea (or insert at caret) with
        // the given prefix/suffix. Used by the Markdown toolbar to apply
        // **bold**, *italic*, etc. without eval'ing strings from C#.
        // placeholder is inserted between prefix+suffix when nothing is
        // selected, so the user immediately sees what was added.
        // Returns true if the textarea was found and updated.
        wrapSelection: function (textareaId, prefix, suffix, placeholder) {
            var el = document.getElementById(textareaId);
            if (!el || typeof el.selectionStart !== 'number') return false;
            var start = el.selectionStart;
            var end = el.selectionEnd;
            var value = el.value;
            var selected = value.substring(start, end);
            var inner = selected.length > 0 ? selected : (placeholder || '');
            var newValue = value.substring(0, start) + prefix + inner + suffix + value.substring(end);
            // Use the input setter so React/Blazor's value tracker still sees a change.
            var setter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;
            setter.call(el, newValue);
            el.dispatchEvent(new Event('input', { bubbles: true }));
            // Re-focus and place the caret around the inserted content.
            var caret = start + prefix.length + inner.length;
            el.focus();
            el.setSelectionRange(caret - inner.length, caret);
            return true;
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
