using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Comment
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public DateTime Date { get; set; }
    public Assignee? Owner { get; set; }
    [JsonPropertyName("comment")]
    public string? Text { get; set; }
    public bool Deleted { get; set; }
}
