using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Assignee
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public string? Title { get; set; }
    public bool Deleted { get; set; }

    /// <summary>Avatar image filename (trudesk serves it from /uploads/users/).</summary>
    public string? Image { get; set; }
}
