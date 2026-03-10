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
    public string? TicketType { get; set; }
    public string? TicketGroup { get; set; }
    public string? TicketPriority { get; set; }
    public string? TicketAssignee { get; set; }
    public List<string> TicketTags { get; set; } = [];
    public string? ScheduleType { get; set; } // monthly, quarterly, annual
    public int DayOfMonth { get; set; }
    public List<int> MonthsOfYear { get; set; } = [];
    public int DaysBeforeDeadline { get; set; }
    public bool Enabled { get; set; }
    public DateTime? NextRun { get; set; }
    public DateTime? LastRun { get; set; }
}
