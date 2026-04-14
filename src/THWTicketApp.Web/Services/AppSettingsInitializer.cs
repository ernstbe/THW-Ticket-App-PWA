using Microsoft.AspNetCore.Components;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class AppSettingsInitializer
{
    private readonly AppSettings _settings;
    private readonly LocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;
    private bool _initialized;

    public AppSettingsInitializer(AppSettings settings, LocalStorageService localStorage, NavigationManager navigationManager)
    {
        _settings = settings;
        _localStorage = localStorage;
        _navigationManager = navigationManager;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var storedUrl = await _localStorage.GetItemAsync("settings_apiurl");
        if (!string.IsNullOrWhiteSpace(storedUrl))
        {
            _settings.ApiBaseUrl = storedUrl;
        }
        else
        {
            _settings.ApiBaseUrl = GetDefaultApiBaseUrl();
        }

        var storedTimeout = await _localStorage.GetItemAsync("settings_timeout");
        if (int.TryParse(storedTimeout, out var timeout) && timeout > 0)
        {
            _settings.ConnectionTimeoutSeconds = timeout;
        }
    }

    public async Task SaveApiUrlAsync(string url)
    {
        _settings.ApiBaseUrl = url.TrimEnd('/');
        await _localStorage.SetItemAsync("settings_apiurl", _settings.ApiBaseUrl);
    }

    private string GetDefaultApiBaseUrl()
    {
        var baseUri = new Uri(_navigationManager.BaseUri);
        return $"{baseUri.Scheme}://{baseUri.Authority}/api/v1";
    }
}
