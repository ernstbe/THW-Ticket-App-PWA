using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Note
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    [JsonPropertyName("owner")]
    public Owner? Owner { get; set; }
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    [JsonPropertyName("note")]
    public string? Content { get; set; }
}
