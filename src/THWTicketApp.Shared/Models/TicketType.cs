using System.Text.Json.Serialization;
using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Shared.Models;

public class TicketType
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    [JsonIgnore]
    public string TranslatedName => Translator.Translate(Name);
    public List<Priority> Priorities { get; set; } = [];
    [JsonPropertyName("__v")]
    public int Version { get; set; }
}
