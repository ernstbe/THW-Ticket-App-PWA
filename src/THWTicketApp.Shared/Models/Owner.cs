using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Owner
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Title { get; set; }
}
