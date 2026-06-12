namespace THWTicketApp.Shared.Models;

/// <summary>
/// A trudesk ticket template as returned by the v2 templates endpoint.
/// Shared DTO for every consumer of the template list (AddTicket picker,
/// Templates admin page, RecurringTasks dialog). Names are the raw server
/// values — translation (e.g. priority names) is presentation logic and
/// stays in the pages.
/// </summary>
public sealed class TicketTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Issue { get; set; }
    public string? TypeId { get; set; }
    public string? TypeName { get; set; }
    public string? PriorityId { get; set; }
    public string? PriorityName { get; set; }
    public List<string> Checklist { get; set; } = new();
}
