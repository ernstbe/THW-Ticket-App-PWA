using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Web.Helpers;

// #privatetickets — a private group's raw Name is "private:<objectid>",
// the internal identifier used server-side, never meant for display. Every
// render site that prints a group name must go through here instead of
// reading Group.Name directly.
public static class GroupDisplayHelper
{
    public static string GetDisplayName(Group? group, LocalizationService localization)
    {
        if (group == null) return string.Empty;
        return group.Private ? localization.T("groups.private_label") : (group.Name ?? string.Empty);
    }

    // Reports (handover) and RecurringTasks templates have no sensible
    // reading of "target my private space" — filter it out entirely rather
    // than showing the raw name there.
    public static List<Group> ExcludePrivate(IEnumerable<Group> groups) =>
        groups.Where(g => !g.Private).ToList();
}
