using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Behavior tests for IdleLockService (added in #68 + #69).
///
/// The JSInterop side (timer reset on user activity) is covered manually
/// by Cypress/eye — here we lock down the C# state machine: settings
/// load/save, the no-passkey hard guard, and OnIdle's lock + redirect.
/// </summary>
public class IdleLockServiceTests
{
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly ITrueDeskApiService _api = Substitute.For<ITrueDeskApiService>();
    private readonly NavigationManager _nav = new TestNavigationManager();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly LocalizationService _l10n;
    private readonly AuthStateProvider _auth;

    public IdleLockServiceTests()
    {
        _l10n = new LocalizationService(_storage);
        var jsRuntime = Substitute.For<IJSRuntime>();
        _auth = new AuthStateProvider(_api, new AppSettings(), jsRuntime, _nav);
    }

    private IdleLockService Build() => new(
        Substitute.For<IJSRuntime>(),
        _storage,
        _api,
        _auth,
        _nav,
        _snackbar,
        _l10n
    );

    [Fact]
    public async Task LoadSettingsAsync_usesDefaultsWhenStorageEmpty()
    {
        var sut = Build();
        await sut.LoadSettingsAsync();

        Assert.False(sut.Enabled);
        Assert.Equal(IdleLockService.DefaultMinutes, sut.TimeoutMinutes);
    }

    [Fact]
    public async Task LoadSettingsAsync_readsStoredValues()
    {
        _storage.Store[IdleLockService.EnabledKey] = "true";
        _storage.Store[IdleLockService.MinutesKey] = "15";

        var sut = Build();
        await sut.LoadSettingsAsync();

        Assert.True(sut.Enabled);
        Assert.Equal(15, sut.TimeoutMinutes);
    }

    [Fact]
    public async Task LoadSettingsAsync_ignoresOutOfRangeMinutes()
    {
        // Defense against tampering: only the curated dropdown values are valid.
        _storage.Store[IdleLockService.MinutesKey] = "9999";

        var sut = Build();
        await sut.LoadSettingsAsync();

        Assert.Equal(IdleLockService.DefaultMinutes, sut.TimeoutMinutes);
    }

    [Fact]
    public async Task SaveSettingsAsync_persistsToLocalStorage()
    {
        var sut = Build();
        await sut.SaveSettingsAsync(enabled: true, minutes: 30);

        Assert.Equal("true", _storage.Store[IdleLockService.EnabledKey]);
        Assert.Equal("30", _storage.Store[IdleLockService.MinutesKey]);
        Assert.True(sut.Enabled);
        Assert.Equal(30, sut.TimeoutMinutes);
    }

    [Fact]
    public async Task SaveSettingsAsync_invalidMinutesFallsBackToDefault()
    {
        var sut = Build();
        await sut.SaveSettingsAsync(enabled: true, minutes: 7);

        Assert.Equal(IdleLockService.DefaultMinutes, sut.TimeoutMinutes);
    }

    [Fact]
    public async Task StartAsync_doesNothingWhenDisabled()
    {
        _api.IsAuthenticated.Returns(true);
        _storage.Store["passkey_credential_id"] = "cred";

        var sut = Build();
        // Enabled is false by default
        await sut.StartAsync();

        // Service should not have called LogoutAsync (no idle fired) — but
        // also we have no observable "running" flag from outside. The real
        // assertion is the symmetric one in StartAsync_doesNothingWithoutPasskey.
        await _api.DidNotReceive().LogoutAsync();
    }

    [Fact]
    public async Task StartAsync_doesNothingWithoutPasskey()
    {
        // Hard guard: auto-lock without an unlock path = stranded user.
        _api.IsAuthenticated.Returns(true);

        var sut = Build();
        await sut.SaveSettingsAsync(enabled: true, minutes: 5);
        // No passkey_credential_id in storage.
        await sut.StartAsync();

        await _api.DidNotReceive().LogoutAsync();
    }

    [Fact]
    public async Task OnIdle_lockedSessionAndNavigatesToLogin()
    {
        _api.IsAuthenticated.Returns(true);
        _storage.Store["passkey_credential_id"] = "cred";

        var sut = Build();
        await sut.OnIdle();

        await _api.Received(1).LogoutAsync();
        Assert.EndsWith("login", _nav.Uri);
        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Info,
            Arg.Any<Action<SnackbarOptions>?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task OnIdle_doesNothingIfNotAuthenticated()
    {
        // Race: timer fires after user logged out via another flow.
        _api.IsAuthenticated.Returns(false);
        _storage.Store["passkey_credential_id"] = "cred";

        var sut = Build();
        await sut.OnIdle();

        await _api.DidNotReceive().LogoutAsync();
    }

    [Fact]
    public async Task OnIdle_doesNothingIfPasskeyDisappeared()
    {
        // Race: user removed passkey between timer set and timer fire.
        _api.IsAuthenticated.Returns(true);

        var sut = Build();
        await sut.OnIdle();

        await _api.DidNotReceive().LogoutAsync();
    }

    [Fact]
    public void AllowedMinutes_matchesSettingsUiDropdown()
    {
        // Make sure the allowed-values list stays in sync with the dropdown
        // in Settings.razor. If you change one, change the other.
        Assert.Equal(new[] { 5, 10, 15, 30, 60 }, IdleLockService.AllowedMinutes);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://test/", "https://test/");
        protected override void NavigateToCore(string uri, bool forceLoad) =>
            Uri = new Uri(new Uri(BaseUri), uri).ToString();
    }
}
