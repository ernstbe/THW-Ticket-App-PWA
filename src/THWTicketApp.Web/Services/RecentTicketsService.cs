using System.Text.Json;

namespace THWTicketApp.Web.Services;

/// <summary>
/// Tracks the user's last-visited tickets in localStorage. Surfaced as
/// a quick-access card on the Dashboard.
///
/// Stored as a JSON array under "recent_tickets" — most-recent first,
/// capped at <see cref="MaxEntries"/>. Each entry carries the minimum
/// needed to render a link without an extra API roundtrip.
/// </summary>
public sealed class RecentTicketsService
{
    public const string StorageKey = "recent_tickets";
    public const int MaxEntries = 8;

    private readonly LocalStorageService _localStorage;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecentTicketsService(LocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<RecentTicket>> GetAsync()
    {
        var raw = await _localStorage.GetItemAsync(StorageKey);
        if (string.IsNullOrEmpty(raw)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<RecentTicket>>(raw, JsonOpts) ?? new();
        }
        catch
        {
            // Corrupted storage — nuke it rather than letting a parser error
            // crash the dashboard on every load.
            await _localStorage.RemoveItemAsync(StorageKey);
            return new();
        }
    }

    public async Task RecordAsync(string uid, string subject, int? statusUid = null)
    {
        if (string.IsNullOrEmpty(uid)) return;

        var list = await GetAsync();
        // Remove any earlier visit of the same ticket so it moves to the top.
        list.RemoveAll(e => string.Equals(e.Uid, uid, StringComparison.Ordinal));
        list.Insert(0, new RecentTicket
        {
            Uid = uid,
            Subject = subject,
            StatusUid = statusUid,
            VisitedAt = DateTimeOffset.UtcNow
        });
        if (list.Count > MaxEntries) list = list.GetRange(0, MaxEntries);

        var json = JsonSerializer.Serialize(list, JsonOpts);
        await _localStorage.SetItemAsync(StorageKey, json);
    }

    public async Task ClearAsync()
    {
        await _localStorage.RemoveItemAsync(StorageKey);
    }
}

public sealed class RecentTicket
{
    public string Uid { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int? StatusUid { get; set; }
    public DateTimeOffset VisitedAt { get; set; }
}
