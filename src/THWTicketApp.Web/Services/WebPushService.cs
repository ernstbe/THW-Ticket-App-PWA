using Microsoft.JSInterop;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

/// <summary>
/// Bridges the browser PushManager into the trudesk backend.
/// BrowserNotificationService covers the local-tab case (Socket.IO ->
/// Notification API while the tab is open); WebPushService covers the
/// remote case where the server pushes to a subscription endpoint even
/// when the tab is closed.
/// </summary>
public class WebPushService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ITrueDeskApiService _api;
    private readonly LocalStorageService _localStorage;
    private IJSObjectReference? _module;

    public WebPushService(IJSRuntime jsRuntime, ITrueDeskApiService api, LocalStorageService localStorage)
    {
        _jsRuntime = jsRuntime;
        _api = api;
        _localStorage = localStorage;

        // Tear down on logout so the *next* user logging in on the same
        // browser doesn't inherit the previous user's push subscription
        // (the server would otherwise keep pushing the old user's events
        // to the new user's device). The hook fires while the api still
        // has a valid token, so the DELETE actually authenticates.
        _api.LoggingOut += OnLoggingOutAsync;
    }

    private async Task OnLoggingOutAsync()
    {
        try { await DisableAsync(); } catch { /* hook must not break logout */ }
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/webpush-interop.js");
        return _module;
    }

    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var m = await GetModuleAsync();
            return await m.InvokeAsync<bool>("isSupported");
        }
        catch { return false; }
    }

    public async Task<string> GetPermissionAsync()
    {
        try
        {
            var m = await GetModuleAsync();
            return await m.InvokeAsync<string>("getPermission");
        }
        catch { return "default"; }
    }

    public async Task<bool> IsSubscribedAsync()
    {
        try
        {
            var m = await GetModuleAsync();
            var sub = await m.InvokeAsync<SubscriptionDto?>("getCurrentSubscription");
            return sub != null && !string.IsNullOrEmpty(sub.Endpoint);
        }
        catch { return false; }
    }

    /// <summary>
    /// Full enable flow: request permission, fetch VAPID public key,
    /// subscribe the PushManager, POST the subscription to the backend.
    /// Returns false (and leaves the user unsubscribed) if any step
    /// fails — caller renders an error/snackbar.
    /// </summary>
    public async Task<bool> EnableAsync()
    {
        // Contract: returns false on any failure. The JS subscribe/permission
        // dance rejects for many normal conditions (NotAllowedError, AbortError
        // from the push service, invalid/mismatched VAPID key, no active service
        // worker, transient push-service failure) and GetModuleAsync can throw if
        // the module fails to load. Those surface as JSException — catch here so
        // callers (Settings/Onboarding, which have no catch) don't hit Blazor's
        // error path (#206).
        try
        {
            var module = await GetModuleAsync();
            if (!await module.InvokeAsync<bool>("isSupported")) return false;

            var perm = await module.InvokeAsync<string>("getPermission");
            if (perm == "default") perm = await module.InvokeAsync<string>("requestPermission");
            if (perm != "granted") return false;

            var vapid = await _api.GetWebPushVapidPublicKeyAsync();
            if (string.IsNullOrEmpty(vapid)) return false;

            var sub = await module.InvokeAsync<SubscriptionDto?>("subscribe", vapid);
            if (sub == null || string.IsNullOrEmpty(sub.Endpoint) || sub.Keys == null) return false;

            var deviceId = await _localStorage.GetItemAsync("device_id");
            var userAgent = await _jsRuntime.InvokeAsync<string>("pwaHelpers.getUserAgent");
            return await _api.SubscribeWebPushAsync(sub.Endpoint, sub.Keys.P256dh ?? "", sub.Keys.Auth ?? "", deviceId, userAgent);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort disable. Tries to remove the subscription on the
    /// server first (so the server stops pushing to an endpoint that's
    /// about to stop listening), then unsubscribes the PushManager
    /// locally — even if the server step failed. Result is `true` iff
    /// the LOCAL unsubscribe succeeded; the server state will heal
    /// itself the next time the server tries to push (404/410 from the
    /// push service triggers our auto-tombstone in webpush.sendToUser).
    /// </summary>
    public async Task<bool> DisableAsync()
    {
        IJSObjectReference module;
        try { module = await GetModuleAsync(); }
        catch { return false; }

        // Step 1: best-effort server-side delete.
        try
        {
            var sub = await module.InvokeAsync<SubscriptionDto?>("getCurrentSubscription");
            if (sub != null && !string.IsNullOrEmpty(sub.Endpoint))
            {
                await _api.UnsubscribeWebPushAsync(sub.Endpoint);
            }
        }
        catch { /* server unreachable — fall through to local unsubscribe anyway */ }

        // Step 2: local unsubscribe. If THIS fails the browser keeps
        // receiving pushes, which is the worse failure mode — so this
        // is the step we report on.
        try
        {
            await module.InvokeAsync<string?>("unsubscribe");
            return true;
        }
        catch { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        _api.LoggingOut -= OnLoggingOutAsync;
        if (_module != null) await _module.DisposeAsync();
    }

    private class SubscriptionDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public KeysDto? Keys { get; set; }
    }

    private class KeysDto
    {
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }
}
