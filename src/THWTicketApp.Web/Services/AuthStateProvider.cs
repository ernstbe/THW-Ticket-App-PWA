using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ITrueDeskApiService _apiService;
    private readonly AppSettings _settings;
    private readonly AppSettingsInitializer _settingsInit;
    private bool _initialized;

    public AuthStateProvider(ITrueDeskApiService apiService, AppSettings settings, AppSettingsInitializer settingsInit)
    {
        _apiService = apiService;
        _settings = settings;
        _settingsInit = settingsInit;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;

            // Settings MUST be loaded before we can restore the session, because
            // TryRestoreSessionAsync needs ApiBaseUrl to build the token-verify
            // URL. Previously settings were only initialized in OnAfterRenderAsync
            // of page components — too late, since the router evaluates auth state
            // before any component renders. This race condition caused every page
            // reload to lose the session.
            await _settingsInit.InitializeAsync();

            if (_settings.IsConfigured)
            {
                try { await _apiService.TryRestoreSessionAsync(); }
                catch { /* API unreachable - stay unauthenticated */ }
            }
        }

        if (_apiService.IsAuthenticated)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, _apiService.CurrentUsername ?? ""),
                new Claim(ClaimTypes.NameIdentifier, _apiService.CurrentUserId ?? ""),
            };
            var identity = new ClaimsIdentity(claims, "TrudeskAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
