using System.Text.Json;
using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Ticket
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public Group? Group { get; set; }
    public bool Deleted { get; set; }
    public TicketType? Type { get; set; }
    public Priority? Priority { get; set; }
    public List<Tag> Tags { get; set; } = new();
    public string? Subject { get; set; }
    public string? Issue { get; set; }
    [JsonIgnore]
    public List<string> SubscriberIds { get; set; } = new();
    [JsonPropertyName("subscribers")]
    public JsonElement? RawSubscribers
    {
        get => null;
        set
        {
            if (value is { ValueKind: JsonValueKind.Array } arr)
            {
                SubscriberIds = new List<string>();
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind == JsonValueKind.String)
                        SubscriberIds.Add(elem.GetString()!);
                    else if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("_id", out var id))
                        SubscriberIds.Add(id.GetString()!);
                }
            }
        }
    }
    public DateTime Date { get; set; }
    public List<Comment> Comments { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<HistoryItem> History { get; set; } = new();
    public Status? Status { get; set; }
    public Owner? Owner { get; set; }
    public int Uid { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Assignee? Assignee { get; set; }
    public DateTime Updated { get; set; }
}
