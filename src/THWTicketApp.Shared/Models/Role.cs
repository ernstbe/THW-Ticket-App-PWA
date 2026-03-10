using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Role
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Normalized { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsAgent { get; set; }
}
