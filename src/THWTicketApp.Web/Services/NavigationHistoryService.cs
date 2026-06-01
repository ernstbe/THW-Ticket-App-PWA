using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace THWTicketApp.Web.Services;

/// <summary>
/// Tracks the last "list" page the user was on so the Ticket-Detail back
/// button can return there (Dashboard / Kanban / filtered Tickets list)
/// instead of always defaulting to /tickets.
/// </summary>
public sealed class NavigationHistoryService : IDisposable
{
    private readonly NavigationManager _navigation;
    private string _lastListUri = "tickets";

    public NavigationHistoryService(NavigationManager navigation)
    {
        _navigation = navigation;
        var initial = navigation.ToBaseRelativePath(navigation.Uri);
        if (!IsTicketDetail(initial))
            _lastListUri = initial;
        navigation.LocationChanged += OnLocationChanged;
    }

    public string LastListUri => _lastListUri;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var rel = _navigation.ToBaseRelativePath(e.Location);
        if (!IsTicketDetail(rel))
            _lastListUri = rel;
    }

    private static bool IsTicketDetail(string rel)
    {
        var idx = rel.IndexOf('?');
        var path = idx >= 0 ? rel[..idx] : rel;
        return path.StartsWith("tickets/", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
