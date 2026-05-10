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
    // Trudesk's getassignees endpoint does NOT include the Mongoose "id" virtual,
    // only "_id". Fall back to InternalId so callers don't get an empty string.
    private string _id = string.Empty;
    public string Id { get => _id.Length > 0 ? _id : InternalId ?? string.Empty; set => _id = value; }
    public List<Group> Groups { get; set; } = [];
}
