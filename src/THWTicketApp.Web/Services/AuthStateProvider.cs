using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ITrueDeskApiService _apiService;
    private readonly AppSettings _settings;
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigation;
    private bool _initialized;

    public AuthStateProvider(ITrueDeskApiService apiService, AppSettings settings, IJSRuntime jsRuntime, NavigationManager navigation)
    {
        _apiService = apiService;
        _settings = settings;
        _jsRuntime = jsRuntime;
        _navigation = navigation;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;

            try
            {
                // Initialize settings AND restore session synchronously.
                // Both MUST complete before the router decides whether to
                // show the page or redirect to login.
                if (_jsRuntime is IJSInProcessRuntime js)
                {
                    // 1) Load API base URL (needed for all subsequent API calls)
                    if (!_settings.IsConfigured)
                    {
                        var storedUrl = js.Invoke<string?>("localStorage.getItem", "settings_apiurl");
                        if (!string.IsNullOrWhiteSpace(storedUrl))
                        {
                            _settings.ApiBaseUrl = storedUrl;
                        }
                        else
                        {
                            var baseUri = new Uri(_navigation.BaseUri);
                            _settings.ApiBaseUrl = $"{baseUri.Scheme}://{baseUri.Authority}/api/v1";
                        }
                    }

                    // 2) Restore auth token
                    await _apiService.TryRestoreSessionAsync();
                }
            }
            catch { /* stay unauthenticated */ }
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
