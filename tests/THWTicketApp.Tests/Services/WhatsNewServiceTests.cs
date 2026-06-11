using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// The "Was ist neu?" dialog must appear exactly once per device after an
/// update with unseen changelog entries — and never on fresh installs
/// (those get the onboarding tour instead) or while the tour is visible.
/// </summary>
public class WhatsNewServiceTests
{
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly OnboardingService _onboarding;
    private readonly WhatsNewService _sut;

    public WhatsNewServiceTests()
    {
        _onboarding = new OnboardingService(_storage);
        _sut = new WhatsNewService(_storage, _onboarding);
    }

    private Task MarkExistingInstallAsync()
        => _storage.SetItemAsync(OnboardingService.StorageKey, "true");

    [Fact]
    public async Task FreshInstall_seedsSilently_withoutShowing()
    {
        // No onboarding flag, no whatsnew marker => brand-new device.
        var fired = 0;
        _sut.StateChanged += () => fired++;

        await _sut.ShowIfUpdatedAsync();

        Assert.False(_sut.IsVisible);
        Assert.Equal(0, fired);
        Assert.Equal(WhatsNewService.LatestId.ToString(), _storage.Store[WhatsNewService.StorageKey]);
    }

    [Fact]
    public async Task ExistingInstallWithoutMarker_showsAllEntries()
    {
        await MarkExistingInstallAsync();
        var fired = 0;
        _sut.StateChanged += () => fired++;

        await _sut.ShowIfUpdatedAsync();

        Assert.True(_sut.IsVisible);
        Assert.Equal(1, fired);
        Assert.Equal(WhatsNewService.Entries.Count, _sut.VisibleEntries.Count);
    }

    [Fact]
    public async Task UpToDateMarker_doesNotShow()
    {
        await MarkExistingInstallAsync();
        _storage.Store[WhatsNewService.StorageKey] = WhatsNewService.LatestId.ToString();

        await _sut.ShowIfUpdatedAsync();

        Assert.False(_sut.IsVisible);
    }

    [Fact]
    public async Task OutdatedMarker_showsOnlyNewerEntries_newestFirst()
    {
        await MarkExistingInstallAsync();
        _storage.Store[WhatsNewService.StorageKey] = "0";

        await _sut.ShowIfUpdatedAsync();

        Assert.True(_sut.IsVisible);
        Assert.All(_sut.VisibleEntries, e => Assert.True(e.Id > 0));
        var ids = _sut.VisibleEntries.Select(e => e.Id).ToList();
        Assert.Equal(ids.OrderByDescending(i => i).ToList(), ids);
    }

    [Fact]
    public async Task OnboardingVisible_seedsInsteadOfStacking()
    {
        // Onboarding tour about to show (e.g. user reset it): no overlay pile-up.
        await _onboarding.ShowIfFirstRunAsync();
        Assert.True(_onboarding.IsVisible);
        // Mark as existing install AFTER the tour became visible, so the
        // fresh-install seed path is not what hides the dialog here.
        await MarkExistingInstallAsync();

        await _sut.ShowIfUpdatedAsync();

        Assert.False(_sut.IsVisible);
        Assert.Equal(WhatsNewService.LatestId.ToString(), _storage.Store[WhatsNewService.StorageKey]);
    }

    [Fact]
    public async Task Dismiss_persistsMarkerAndHides()
    {
        await MarkExistingInstallAsync();
        await _sut.ShowIfUpdatedAsync();
        Assert.True(_sut.IsVisible);

        await _sut.DismissAsync();

        Assert.False(_sut.IsVisible);
        Assert.Equal(WhatsNewService.LatestId.ToString(), _storage.Store[WhatsNewService.StorageKey]);

        // A second pass after dismissal stays quiet.
        await _sut.ShowIfUpdatedAsync();
        Assert.False(_sut.IsVisible);
    }

    [Fact]
    public async Task ShowAll_opensFullHistoryRegardlessOfMarker()
    {
        _storage.Store[WhatsNewService.StorageKey] = WhatsNewService.LatestId.ToString();

        _sut.ShowAll();

        Assert.True(_sut.IsVisible);
        Assert.Equal(WhatsNewService.Entries.Count, _sut.VisibleEntries.Count);
        await Task.CompletedTask;
    }

    [Fact]
    public void Entries_haveStrictlyIncreasingUniqueIds()
    {
        // The Id ordering is what the seen-marker logic relies on.
        var ids = WhatsNewService.Entries.Select(e => e.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.Equal(ids.OrderBy(i => i).ToList(), ids);
        Assert.All(WhatsNewService.Entries, e => Assert.NotEmpty(e.Items));
    }
}
