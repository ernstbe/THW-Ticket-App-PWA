using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Behaviour of the once-per-device onboarding tour gate.
/// </summary>
public class OnboardingServiceTests
{
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly OnboardingService _sut;

    public OnboardingServiceTests()
    {
        _sut = new OnboardingService(_storage);
    }

    [Fact]
    public async Task ShowIfFirstRunAsync_emptyStorage_makesVisibleAndFires()
    {
        int fired = 0;
        _sut.StateChanged += () => fired++;

        await _sut.ShowIfFirstRunAsync();

        Assert.True(_sut.IsVisible);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task ShowIfFirstRunAsync_alreadyCompleted_doesNothing()
    {
        _storage.Store[OnboardingService.StorageKey] = "true";

        await _sut.ShowIfFirstRunAsync();

        Assert.False(_sut.IsVisible);
    }

    [Fact]
    public async Task CompleteAsync_persistsAndHides()
    {
        await _sut.ShowIfFirstRunAsync();
        await _sut.CompleteAsync();

        Assert.False(_sut.IsVisible);
        Assert.Equal("true", _storage.Store[OnboardingService.StorageKey]);
        Assert.True(await _sut.HasCompletedAsync());
    }

    [Fact]
    public async Task SkipAsync_persistsLikeComplete()
    {
        await _sut.ShowIfFirstRunAsync();
        await _sut.SkipAsync();

        Assert.False(_sut.IsVisible);
        Assert.True(await _sut.HasCompletedAsync());
    }

    [Fact]
    public async Task ResetAsync_clearsFlag()
    {
        _storage.Store[OnboardingService.StorageKey] = "true";

        await _sut.ResetAsync();

        Assert.False(_storage.Store.ContainsKey(OnboardingService.StorageKey));
        Assert.False(await _sut.HasCompletedAsync());
    }

    [Fact]
    public void ShowNow_ignoresStoredState()
    {
        // The "Show intro again" button in Settings must work even after
        // the user has completed the tour.
        _storage.Store[OnboardingService.StorageKey] = "true";

        _sut.ShowNow();

        Assert.True(_sut.IsVisible);
    }

    [Fact]
    public async Task ShowNow_doesNotMarkAsIncomplete()
    {
        // Re-opening the tour from Settings shouldn't reset the "done"
        // flag — closing it after a re-view still leaves it as completed.
        _storage.Store[OnboardingService.StorageKey] = "true";

        _sut.ShowNow();

        Assert.True(await _sut.HasCompletedAsync());
    }
}
