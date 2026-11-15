using System.Text.Json;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

/// <summary>
/// #favorites — the user's starred tickets. The list lives on the server
/// (per user, via /api/v2/accounts/me/favorites) so it is the same on phone
/// and desktop; this service caches it for the session.
///
/// Earlier builds kept favorites only in this device's localStorage under
/// <see cref="LegacyStorageKey"/>. The first successful load uploads those
/// entries once and then removes the key. Entries the server rejects (ticket
/// deleted or no longer visible) are dropped, which matches what the old
/// Favorites page showed for them anyway.
/// </summary>
public sealed class FavoritesService
{
    public const string LegacyStorageKey = "favorite_tickets";

    private readonly ITrueDeskApiService _api;
    private readonly LocalStorageService _localStorage;
    private List<int>? _cache;
    private bool _legacyMigrated;

    public FavoritesService(ITrueDeskApiService api, LocalStorageService localStorage)
    {
        _api = api;
        _localStorage = localStorage;
    }

    /// <summary>Starred ticket uids. Throws <see cref="FavoritesUnavailableException"/> if the server can't be reached.</summary>
    public async Task<IReadOnlyList<int>> GetAsync(bool refresh = false)
    {
        if (_cache != null && !refresh) return _cache;

        _cache = await _api.GetFavoriteUidsAsync() ?? throw new FavoritesUnavailableException();
        await MigrateLegacyAsync();
        return _cache;
    }

    public async Task<bool> IsFavoriteAsync(int ticketUid) => (await GetAsync()).Contains(ticketUid);

    /// <summary>Stars or unstars a ticket. Returns false if the server rejected or didn't receive the change.</summary>
    public async Task<bool> SetAsync(int ticketUid, bool favorite)
    {
        var updated = favorite
            ? await _api.AddFavoriteAsync(ticketUid)
            : await _api.RemoveFavoriteAsync(ticketUid);
        if (updated == null) return false;
        _cache = updated;
        return true;
    }

    private async Task MigrateLegacyAsync()
    {
        if (_legacyMigrated) return;
        _legacyMigrated = true;

        var json = await _localStorage.GetItemAsync(LegacyStorageKey);
        if (string.IsNullOrEmpty(json)) return;

        List<int> local;
        try { local = JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch (JsonException) { local = []; }

        foreach (var uid in local.Distinct().Where(uid => !_cache!.Contains(uid)).ToList())
        {
            var updated = await _api.AddFavoriteAsync(uid);
            if (updated != null) _cache = updated;
        }

        // GetFavoriteUidsAsync just succeeded, so a failed add here is a
        // server-side "no" (deleted / not visible), not a network blip.
        await _localStorage.RemoveItemAsync(LegacyStorageKey);
    }
}

public sealed class FavoritesUnavailableException : Exception
{
    public FavoritesUnavailableException() : base("Favorites could not be loaded from the server.") { }
}
