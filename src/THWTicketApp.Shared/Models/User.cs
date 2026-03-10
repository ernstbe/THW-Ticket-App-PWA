using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class User
{
    [JsonPropertyName("_id")]
    public string? InternalId { get; set; }
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public Role? Role { get; set; }
    public string? Title { get; set; }
    public bool HasL2Auth { get; set; }
    public bool Deleted { get; set; }
    public DateTime LastOnline { get; set; }
    public string Id { get; set; } = string.Empty;
    public List<Group> Groups { get; set; } = [];
}
