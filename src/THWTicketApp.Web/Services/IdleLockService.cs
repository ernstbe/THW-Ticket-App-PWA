using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

/// <summary>
/// Watches for user inactivity and triggers <see cref="ITrueDeskApiService.LogoutAsync"/>
/// after a configurable timeout. Logout in turn locks the session (keeps the
/// token in <c>locked_auth_*</c> keys) when a passkey is registered, so the
/// user can unlock biometrically without re-entering credentials.
///
/// If no passkey is registered the watcher does nothing — kicking the user
/// out without a way back in would be hostile.
///
/// Persists two settings in localStorage:
///   - <c>settings_idle_lock_enabled</c>: "true" / "false" (default false)
///   - <c>settings_idle_lock_minutes</c>: integer minutes (default 10)
/// </summary>
public sealed class IdleLockService : IAsyncDisposable
{
    public const string EnabledKey = "settings_idle_lock_enabled";
    public const string MinutesKey = "settings_idle_lock_minutes";
    public const int DefaultMinutes = 10;
    public static readonly int[] AllowedMinutes = [5, 10, 15, 30, 60];

    private readonly IJSRuntime _jsRuntime;
    private readonly LocalStorageService _localStorage;
    private readonly ITrueDeskApiService _apiService;
    private readonly AuthStateProvider _authState;
    private readonly NavigationManager _navigation;
    private readonly ISnackbar _snackbar;
    private readonly LocalizationService _localization;

    private IJSObjectReference? _module;
    private DotNetObjectReference<IdleLockService>? _selfRef;
    private bool _watching;

    public bool Enabled { get; private set; }
    public int TimeoutMinutes { get; private set; } = DefaultMinutes;

    public IdleLockService(
        IJSRuntime jsRuntime,
        LocalStorageService localStorage,
        ITrueDeskApiService apiService,
        AuthStateProvider authState,
        NavigationManager navigation,
        ISnackbar snackbar,
        LocalizationService localization)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
        _apiService = apiService;
        _authState = authState;
        _navigation = navigation;
        _snackbar = snackbar;
        _localization = localization;

        // Re-evaluate on every auth-state transition so a fresh login on the
        // same tab restarts the watcher. Without this, the watcher only ever
        // starts on the first MainLayout render — a logout-then-login round
        // trip leaves the user idle-unprotected until they reload the page.
        _authState.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _ = HandleAuthChangeAsync(task);
    }

    private async Task HandleAuthChangeAsync(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task;
            var isAuthed = state.User?.Identity?.IsAuthenticated == true;
            if (isAuthed)
            {
                // StartAsync internally no-ops when not enabled or no passkey.
                if (!_watching) await StartAsync();
            }
            else
            {
                if (_watching) await StopAsync();
            }
        }
        catch
        {
            // Auth-state observation must not throw into Blazor's event loop.
        }
    }

    public async Task LoadSettingsAsync()
    {
        var enabledRaw = await _localStorage.GetItemAsync(EnabledKey);
        Enabled = enabledRaw == "true";

        var minutesRaw = await _localStorage.GetItemAsync(MinutesKey);
        if (int.TryParse(minutesRaw, out var m) && AllowedMinutes.Contains(m))
            TimeoutMinutes = m;
        else
            TimeoutMinutes = DefaultMinutes;
    }

    public async Task SaveSettingsAsync(bool enabled, int minutes)
    {
        if (!AllowedMinutes.Contains(minutes)) minutes = DefaultMinutes;
        Enabled = enabled;
        TimeoutMinutes = minutes;
        await _localStorage.SetItemAsync(EnabledKey, enabled ? "true" : "false");
        await _localStorage.SetItemAsync(MinutesKey, minutes.ToString());

        // Apply immediately to a running watcher; the layout will start/stop us
        // on auth-state changes.
        if (_watching)
        {
            if (enabled)
                await UpdateTimeoutAsync();
            else
                await StopAsync();
        }
        else if (enabled && _apiService.IsAuthenticated)
        {
            await StartAsync();
        }
    }

    public async Task StartAsync()
    {
        if (!Enabled || !_apiService.IsAuthenticated) return;

        // Skip if no passkey — auto-lock without unlock path = user gets stuck on
        // the login screen and has to type their password again. Defeats the point.
        var passkeyId = await _localStorage.GetItemAsync("passkey_credential_id");
        if (string.IsNullOrEmpty(passkeyId)) return;

        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/idle-lock-interop.js");
            _selfRef ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("startIdleWatch", _selfRef, TimeoutMinutes);
            _watching = true;
        }
        catch
        {
            // JSInterop failures are non-fatal — worst case is no auto-lock.
        }
    }

    public async Task UpdateTimeoutAsync()
    {
        if (_module == null) return;
        try { await _module.InvokeVoidAsync("updateTimeout", TimeoutMinutes); }
        catch { }
    }

    public async Task StopAsync()
    {
        if (_module == null) return;
        try { await _module.InvokeVoidAsync("stopIdleWatch"); }
        catch { }
        _watching = false;
    }

    [JSInvokable]
    public async Task OnIdle()
    {
        // Double-check we still have a passkey and are authenticated before
        // pulling the rug out.
        if (!_apiService.IsAuthenticated) return;

        var passkeyId = await _localStorage.GetItemAsync("passkey_credential_id");
        if (string.IsNullOrEmpty(passkeyId)) return;

        await StopAsync();
        await _apiService.LogoutAsync(); // locks because passkey is registered
        _authState.NotifyAuthStateChanged();
        _snackbar.Add(_localization.T("idlelock.locked"), Severity.Info);
        _navigation.NavigateTo("login");
    }

    public async ValueTask DisposeAsync()
    {
        _authState.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        await StopAsync();
        _selfRef?.Dispose();
        if (_module != null)
        {
            try { await _module.DisposeAsync(); } catch { }
        }
    }
}
