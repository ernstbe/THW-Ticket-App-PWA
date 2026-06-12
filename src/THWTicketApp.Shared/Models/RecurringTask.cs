using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class RecurringTask
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? TicketSubject { get; set; }
    public string? TicketIssue { get; set; }

    // trudesk's getAll() populates the ref fields below as objects
    // ({_id, name, ...}), but raw/unpopulated documents may carry plain
    // ObjectId strings — PopulatedRefConverter tolerates both.
    public PopulatedRef? TicketType { get; set; }
    public PopulatedRef? TicketGroup { get; set; }
    public PopulatedRef? TicketPriority { get; set; }
    public PopulatedRef? TicketAssignee { get; set; }
    public List<PopulatedRef>? TicketTags { get; set; }

    public List<RecurringTaskChecklistItem> Checklist { get; set; } = [];
    public string? ScheduleType { get; set; } // monthly, quarterly, annual
    public int DayOfMonth { get; set; }
    public List<int> MonthsOfYear { get; set; } = [];
    public int DaysBeforeDeadline { get; set; }
    public bool Enabled { get; set; }
    public DateTime? NextRun { get; set; }
    public DateTime? LastRun { get; set; }
}

public class RecurringTaskChecklistItem
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Title { get; set; }
}
