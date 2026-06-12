using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Services;

/// <summary>
/// Shared, cached access to reference lookups that several pages need
/// (ticket types and the priorities attached to them). Replaces the
/// per-page "GetTicketTypesAsync → Deserialize → SelectMany(Priorities)"
/// copies that had drifted apart in how they translated priority names.
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Loads all ticket types plus the de-duplicated union of every
    /// priority configured on any type. The result is cached for the
    /// lifetime of the scope (Blazor WASM: effectively the session) —
    /// concurrent and repeat callers share one API call. Failures are
    /// NOT cached; the next call retries.
    ///
    /// `Name` on the returned models is the raw server value — callers
    /// must render via <c>TranslatedName</c> (generic
    /// <see cref="THWTicketApp.Shared.Helpers.Translator"/>) and never
    /// mutate the cached instances.
    /// </summary>
    Task<(IReadOnlyList<TicketType> Types, IReadOnlyList<Priority> Priorities)> GetTypesAndPrioritiesAsync();

    /// <summary>Drops the cache; also invoked automatically on logout.</summary>
    void Reset();
}
