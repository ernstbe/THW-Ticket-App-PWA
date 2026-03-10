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
            await _apiService.TryRestoreSessionAsync();
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
