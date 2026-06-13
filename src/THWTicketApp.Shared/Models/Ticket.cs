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
    public DateTime Date { get; set; }
    public List<Comment> Comments { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<HistoryItem> History { get; set; } = new();
    public List<ChecklistItem> Checklist { get; set; } = new();
    public Status? Status { get; set; }
    public Owner? Owner { get; set; }
    public int Uid { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Assignee? Assignee { get; set; }
    /// <summary>
    /// "Weitere Zuständige" besides the primary <see cref="Assignee"/>.
    /// Populated user objects, same shape as the assignee field.
    /// Missing/empty on tickets created before the feature existed.
    /// </summary>
    [JsonPropertyName("additionalAssignees")]
    public List<Assignee> AdditionalAssignees { get; set; } = new();

    /// <summary>
    /// Bidirectional links to other tickets (trudesk v2 feature). The server
    /// populates each entry's <see cref="LinkedTicket.Ticket"/> with uid,
    /// subject and status. Empty on tickets without links.
    /// </summary>
    [JsonPropertyName("linkedTickets")]
    public List<TicketLink> LinkedTickets { get; set; } = new();
    public DateTime Updated { get; set; }

    /// <summary>
    /// Falls back to <see cref="Date"/> when <see cref="Updated"/> is
    /// unset (DateTime.MinValue). Pre-PR-100 tickets and older trudesk
    /// versions never stamped `updated`, which would otherwise dump them
    /// to the bottom of "Zuletzt aktualisiert" sorting.
    /// </summary>
    [JsonIgnore]
    public DateTime LastActivityAt => Updated == DateTime.MinValue ? Date : Updated;
}
