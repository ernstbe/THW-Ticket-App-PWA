using System.Text.Json;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

/// <summary>
/// Scoped (≈ session-wide in Blazor WASM) cache for the ticket-type /
/// priority reference data. One API call serves AddTicket, TicketDetail,
/// Templates and RecurringTasks instead of four identical fetch+parse
/// copies. The cached <c>Name</c> values stay untranslated — pages render
/// <c>TranslatedName</c>, which keeps every page on the same (generic)
/// translation table.
/// </summary>
public sealed class LookupService : ILookupService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ITrueDeskApiService _api;

    // Task cache: concurrent first callers share the same in-flight
    // request. Faulted loads clear themselves so the next call retries.
    private Task<(IReadOnlyList<TicketType> Types, IReadOnlyList<Priority> Priorities)>? _cache;

    public LookupService(ITrueDeskApiService api)
    {
        _api = api;
        // Reference data is the same for every user today, but clearing on
        // logout keeps the service correct if that ever changes (and frees
        // the cache on the login screen).
        _api.LoggingOut += OnLoggingOutAsync;
    }

    public Task<(IReadOnlyList<TicketType> Types, IReadOnlyList<Priority> Priorities)> GetTypesAndPrioritiesAsync()
    {
        // A faulted/cancelled load is never kept — the next caller retries.
        if (_cache is { IsFaulted: false, IsCanceled: false } cached) return cached;
        return _cache = LoadAsync();
    }

    public void Reset() => _cache = null;

    private async Task<(IReadOnlyList<TicketType> Types, IReadOnlyList<Priority> Priorities)> LoadAsync()
    {
        var json = await _api.GetTicketTypesAsync();
        IReadOnlyList<TicketType> types = JsonSerializer.Deserialize<TicketType[]>(json, JsonOptions) ?? [];
        IReadOnlyList<Priority> priorities = types
            .SelectMany(t => t.Priorities)
            .DistinctBy(p => p.Id)
            .ToList();
        return (types, priorities);
    }

    private Task OnLoggingOutAsync()
    {
        Reset();
        return Task.CompletedTask;
    }

    public void Dispose() => _api.LoggingOut -= OnLoggingOutAsync;
}
