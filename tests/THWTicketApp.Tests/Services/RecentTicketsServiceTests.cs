using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Behaviour of the "Recently visited" tracker shown on the Dashboard.
/// </summary>
public class RecentTicketsServiceTests
{
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly RecentTicketsService _sut;

    public RecentTicketsServiceTests()
    {
        _sut = new RecentTicketsService(_storage);
    }

    [Fact]
    public async Task GetAsync_emptyStorage_returnsEmptyList()
    {
        var list = await _sut.GetAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task RecordAsync_persistsAndRoundtrips()
    {
        await _sut.RecordAsync("100", "Pumpe defekt", statusUid: 1);
        var list = await _sut.GetAsync();

        Assert.Single(list);
        Assert.Equal("100", list[0].Uid);
        Assert.Equal("Pumpe defekt", list[0].Subject);
        Assert.Equal(1, list[0].StatusUid);
    }

    [Fact]
    public async Task RecordAsync_repeatedVisit_movesToTopAndDedupes()
    {
        await _sut.RecordAsync("100", "First");
        await _sut.RecordAsync("200", "Second");
        await _sut.RecordAsync("100", "First updated");

        var list = await _sut.GetAsync();
        Assert.Equal(2, list.Count);
        // 100 should now be on top, with the updated subject.
        Assert.Equal("100", list[0].Uid);
        Assert.Equal("First updated", list[0].Subject);
        Assert.Equal("200", list[1].Uid);
    }

    [Fact]
    public async Task RecordAsync_capsAtMaxEntries()
    {
        for (var i = 0; i < RecentTicketsService.MaxEntries + 5; i++)
            await _sut.RecordAsync($"t{i}", $"Ticket {i}");

        var list = await _sut.GetAsync();
        Assert.Equal(RecentTicketsService.MaxEntries, list.Count);
        // Newest first — last inserted should be the head.
        Assert.Equal($"t{RecentTicketsService.MaxEntries + 4}", list[0].Uid);
    }

    [Fact]
    public async Task RecordAsync_emptyUid_isNoOp()
    {
        await _sut.RecordAsync("", "Garbage");
        Assert.Empty(await _sut.GetAsync());
    }

    [Fact]
    public async Task ClearAsync_emptiesStorage()
    {
        await _sut.RecordAsync("1", "x");
        await _sut.RecordAsync("2", "y");

        await _sut.ClearAsync();

        Assert.Empty(await _sut.GetAsync());
        Assert.False(_storage.Store.ContainsKey(RecentTicketsService.StorageKey));
    }

    [Fact]
    public async Task GetAsync_corruptedStorage_returnsEmptyAndClearsKey()
    {
        // Simulate someone (or a future format change) writing garbage.
        // Service must not crash the dashboard.
        _storage.Store[RecentTicketsService.StorageKey] = "{not valid json[";

        var list = await _sut.GetAsync();

        Assert.Empty(list);
        // The corrupted entry should be cleaned up so it doesn't keep firing.
        Assert.False(_storage.Store.ContainsKey(RecentTicketsService.StorageKey));
    }
}
