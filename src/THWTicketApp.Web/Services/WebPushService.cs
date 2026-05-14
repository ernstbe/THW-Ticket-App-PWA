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
        var userAgent = await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
        return await _api.SubscribeWebPushAsync(sub.Endpoint, sub.Keys.P256dh ?? "", sub.Keys.Auth ?? "", deviceId, userAgent);
    }

    /// <summary>
    /// Best-effort disable: tell the backend to forget the subscription,
    /// then unsubscribe on the browser side. We do server first so a
    /// transient network error doesn't leave the server pushing to a
    /// browser that's stopped listening.
    /// </summary>
    public async Task<bool> DisableAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            var sub = await module.InvokeAsync<SubscriptionDto?>("getCurrentSubscription");
            if (sub != null && !string.IsNullOrEmpty(sub.Endpoint))
            {
                await _api.UnsubscribeWebPushAsync(sub.Endpoint);
            }
            await module.InvokeAsync<string?>("unsubscribe");
            return true;
        }
        catch { return false; }
    }

    public async ValueTask DisposeAsync()
    {
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
