using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Tag
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Normalized { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
}
