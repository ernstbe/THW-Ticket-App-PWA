using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Group
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool Public { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
}
