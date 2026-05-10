using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ITrueDeskApiService _apiService;
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public AuthStateProvider(ITrueDeskApiService apiService, IJSRuntime jsRuntime)
    {
        _apiService = apiService;
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;

            try
            {
                await _apiService.TryRestoreSessionAsync();
                Log($"[AUTH] Restore done. IsAuthenticated={_apiService.IsAuthenticated}, User={_apiService.CurrentUsername}");
            }
            catch (Exception ex)
            {
                Log($"[AUTH] Restore FAILED: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (_apiService.IsAuthenticated)
        {
            Log($"[AUTH] Returning authenticated state for {_apiService.CurrentUsername}");
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, _apiService.CurrentUsername ?? ""),
                new Claim(ClaimTypes.NameIdentifier, _apiService.CurrentUserId ?? ""),
            };
            var identity = new ClaimsIdentity(claims, "TrudeskAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        Log("[AUTH] Returning ANONYMOUS state");
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private void Log(string message)
    {
        try
        {
            if (_jsRuntime is IJSInProcessRuntime js)
                js.InvokeVoid("console.log", message);
        }
        catch { /* ignore logging failures */ }
    }
}
