using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ITrueDeskApiService _apiService;
    private bool _initialized;

    public AuthStateProvider(ITrueDeskApiService apiService)
    {
        _apiService = apiService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;

            // Always try to restore the session from localStorage.
            // TryRestoreSessionAsync no longer needs ApiBaseUrl (the verify
            // round-trip was removed in PR #56) — it just reads the stored
            // token and trusts it. So we don't need settings to be initialized
            // first, removing the race condition that caused session loss on
            // every page reload.
            try { await _apiService.TryRestoreSessionAsync(); }
            catch { /* localStorage not ready or empty — stay unauthenticated */ }
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
