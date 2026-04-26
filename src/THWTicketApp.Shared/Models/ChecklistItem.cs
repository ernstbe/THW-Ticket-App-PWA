using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class ChecklistItem
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
}
