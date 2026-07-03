using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Central, resilient classification of a ticket status as active vs closed.
///
/// The "Aktiv" ticket filter used to key purely off <see cref="Status.IsResolved"/>.
/// If a trudesk status has that flag mis-configured (isResolved=true on a
/// non-closed status), the whole active list collapsed to zero even though the
/// dashboard — which counts open/in-progress by name/flag — still showed them
/// (frontend review BUG-2). We therefore classify by the status NAME first
/// (which trudesk/THW use conventionally, incl. the German translations), and
/// only fall back to the isResolved/isInProgress flags for unknown custom names.
/// A clearly active status is never reported as closed.
/// </summary>
public static class TicketStatusHelper
{
    // Names (translated or raw) that unambiguously mean the ticket is still
    // being worked — never closed, whatever the isResolved flag says.
    private static readonly HashSet<string> ActiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "offen", "open", "neu", "new",
        "in bearbeitung", "in progress",
        "ausstehend", "pending",
        "wartend", "on hold"
    };

    private static readonly HashSet<string> ClosedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "geschlossen", "closed", "gelöst", "resolved"
    };

    private static readonly HashSet<string> OpenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "offen", "open", "neu", "new"
    };

    public static bool IsClosed(Status? status)
    {
        if (status == null) return false;

        var name = status.Name;
        if (!string.IsNullOrEmpty(name))
        {
            if (ActiveNames.Contains(name)) return false;
            if (ClosedNames.Contains(name)) return true;
        }

        // Unknown custom status name: trust the flags, but never call an
        // in-progress status closed.
        if (status.IsInProgress) return false;
        return status.IsResolved;
    }

    public static bool IsActive(Status? status) => !IsClosed(status);

    /// <summary>True for the "Offen"/"Neu" (open/new) bucket specifically.</summary>
    public static bool IsOpen(Status? status) =>
        status?.Name is { Length: > 0 } name && OpenNames.Contains(name);
}
