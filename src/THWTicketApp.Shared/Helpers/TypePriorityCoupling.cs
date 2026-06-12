using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Pure helpers that couple the priority dropdown to the selected ticket
/// type (AddTicket + RecurringTasks dialogs). Trudesk configures a
/// priority list per type; the server does not validate the combination
/// on create, and tickets with a priority the type doesn't know can't be
/// edited properly in the trudesk web UI — so the client restricts the
/// choice up front. Kept static and side-effect-free so the logic is
/// unit-testable without bUnit.
/// </summary>
public static class TypePriorityCoupling
{
    /// <summary>
    /// The priorities selectable for <paramref name="type"/>: the type's
    /// own list, or the cross-type union as fallback when no type is
    /// selected or the type defines no priorities of its own.
    /// </summary>
    public static List<Priority> PrioritiesForType(TicketType? type, IReadOnlyList<Priority> allPriorities)
        => type?.Priorities is { Count: > 0 } own ? own.ToList() : allPriorities.ToList();

    /// <summary>
    /// Keeps <paramref name="selectedId"/> only when it is still part of
    /// <paramref name="available"/>; otherwise the selection is cleared.
    /// Matching is by Id — names are display-only.
    /// </summary>
    public static string? EnsureSelectedId(IReadOnlyList<Priority> available, string? selectedId)
        => selectedId != null && available.Any(p => p.Id == selectedId) ? selectedId : null;

    /// <summary>
    /// Object-reference variant for selects bound to <see cref="Priority"/>
    /// instances: re-maps the current selection onto the instance contained
    /// in <paramref name="available"/> (MudSelect matches items by
    /// reference), or returns null when the priority is no longer offered.
    /// </summary>
    public static Priority? EnsureSelected(IReadOnlyList<Priority> available, Priority? selected)
        => selected?.Id == null ? null : available.FirstOrDefault(p => p.Id == selected.Id);
}
