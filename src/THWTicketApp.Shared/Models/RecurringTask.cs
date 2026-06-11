using System.Text.Json;
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
    // ObjectId strings — tolerate both (same pattern as Owner.Role).
    [JsonIgnore]
    public string? TicketTypeId { get; set; }
    [JsonIgnore]
    public string? TicketTypeName { get; set; }
    public JsonElement? TicketType
    {
        get => null;
        set => (TicketTypeId, TicketTypeName) = ExtractRef(value);
    }

    [JsonIgnore]
    public string? TicketGroupId { get; set; }
    [JsonIgnore]
    public string? TicketGroupName { get; set; }
    public JsonElement? TicketGroup
    {
        get => null;
        set => (TicketGroupId, TicketGroupName) = ExtractRef(value);
    }

    [JsonIgnore]
    public string? TicketPriorityId { get; set; }
    [JsonIgnore]
    public string? TicketPriorityName { get; set; }
    public JsonElement? TicketPriority
    {
        get => null;
        set => (TicketPriorityId, TicketPriorityName) = ExtractRef(value);
    }

    [JsonIgnore]
    public string? TicketAssigneeId { get; set; }
    public JsonElement? TicketAssignee
    {
        get => null;
        set => (TicketAssigneeId, _) = ExtractRef(value);
    }

    [JsonIgnore]
    public List<string> TicketTagIds { get; set; } = [];
    public JsonElement? TicketTags
    {
        get => null;
        set
        {
            if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Array) return;
            TicketTagIds = value.Value.EnumerateArray()
                .Select(el => ExtractRef(el).id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .ToList();
        }
    }

    public List<RecurringTaskChecklistItem> Checklist { get; set; } = [];
    public string? ScheduleType { get; set; } // monthly, quarterly, annual
    public int DayOfMonth { get; set; }
    public List<int> MonthsOfYear { get; set; } = [];
    public int DaysBeforeDeadline { get; set; }
    public bool Enabled { get; set; }
    public DateTime? NextRun { get; set; }
    public DateTime? LastRun { get; set; }

    private static (string? id, string? name) ExtractRef(JsonElement? element)
    {
        if (!element.HasValue) return (null, null);
        return ExtractRef(element.Value);
    }

    private static (string? id, string? name) ExtractRef(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return (element.GetString(), null);
        if (element.ValueKind == JsonValueKind.Object)
        {
            var id = element.TryGetProperty("_id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;
            var name = element.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;
            return (id, name);
        }
        return (null, null);
    }
}

public class RecurringTaskChecklistItem
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Title { get; set; }
}
