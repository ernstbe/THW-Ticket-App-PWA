using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// #favorites — server-backed favorites, incl. the one-time upload of the
/// stars older builds kept in this device's localStorage.
/// </summary>
public class FavoritesServiceTests
{
    private readonly ITrueDeskApiService _api = Substitute.For<ITrueDeskApiService>();
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly FavoritesService _sut;

    public FavoritesServiceTests()
    {
        _sut = new FavoritesService(_api, _storage);
    }

    [Fact]
    public async Task GetAsync_returnsServerList_andCachesIt()
    {
        _api.GetFavoriteUidsAsync().Returns(new List<int> { 7, 3 });

        Assert.Equal(new[] { 7, 3 }, await _sut.GetAsync());
        Assert.Equal(new[] { 7, 3 }, await _sut.GetAsync());

        await _api.Received(1).GetFavoriteUidsAsync();
    }

    [Fact]
    public async Task GetAsync_serverUnreachable_throws()
    {
        _api.GetFavoriteUidsAsync().Returns((List<int>?)null);

        await Assert.ThrowsAsync<FavoritesUnavailableException>(() => _sut.GetAsync());
    }

    [Fact]
    public async Task GetAsync_uploadsLegacyLocalFavoritesOnce_thenRemovesKey()
    {
        _storage.Store[FavoritesService.LegacyStorageKey] = "[3,5,5,9]";
        _api.GetFavoriteUidsAsync().Returns(new List<int> { 3 });
        _api.AddFavoriteAsync(5).Returns(new List<int> { 3, 5 });
        _api.AddFavoriteAsync(9).Returns((List<int>?)null); // e.g. ticket deleted -> 404

        var result = await _sut.GetAsync();

        Assert.Equal(new[] { 3, 5 }, result);
        await _api.DidNotReceive().AddFavoriteAsync(3); // already on the server
        await _api.Received(1).AddFavoriteAsync(5);    // de-duplicated
        Assert.False(_storage.Store.ContainsKey(FavoritesService.LegacyStorageKey));

        await _sut.GetAsync(refresh: true);
        await _api.Received(1).AddFavoriteAsync(5);    // not uploaded again
    }

    [Fact]
    public async Task GetAsync_serverUnreachable_keepsLegacyKeyForNextTime()
    {
        _storage.Store[FavoritesService.LegacyStorageKey] = "[5]";
        _api.GetFavoriteUidsAsync().Returns((List<int>?)null);

        await Assert.ThrowsAsync<FavoritesUnavailableException>(() => _sut.GetAsync());

        Assert.True(_storage.Store.ContainsKey(FavoritesService.LegacyStorageKey));
        await _api.DidNotReceive().AddFavoriteAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task GetAsync_corruptLegacyValue_isDropped()
    {
        _storage.Store[FavoritesService.LegacyStorageKey] = "not json";
        _api.GetFavoriteUidsAsync().Returns(new List<int>());

        Assert.Empty(await _sut.GetAsync());
        Assert.False(_storage.Store.ContainsKey(FavoritesService.LegacyStorageKey));
    }

    [Fact]
    public async Task SetAsync_updatesCacheFromServerResponse()
    {
        _api.GetFavoriteUidsAsync().Returns(new List<int>());
        _api.AddFavoriteAsync(4).Returns(new List<int> { 4 });
        _api.RemoveFavoriteAsync(4).Returns(new List<int>());

        Assert.True(await _sut.SetAsync(4, true));
        Assert.True(await _sut.IsFavoriteAsync(4));

        Assert.True(await _sut.SetAsync(4, false));
        Assert.False(await _sut.IsFavoriteAsync(4));
    }

    [Fact]
    public async Task SetAsync_failure_returnsFalse_andKeepsState()
    {
        _api.GetFavoriteUidsAsync().Returns(new List<int> { 4 });
        _api.RemoveFavoriteAsync(4).Returns((List<int>?)null);

        Assert.False(await _sut.SetAsync(4, false));
        Assert.True(await _sut.IsFavoriteAsync(4));
    }
}
