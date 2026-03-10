using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Asset
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AssetTag { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public List<string> LinkedTickets { get; set; } = [];
}
