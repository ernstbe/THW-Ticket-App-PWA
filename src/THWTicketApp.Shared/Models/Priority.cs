using System.Text.Json.Serialization;
using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Shared.Models;

public class Priority
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }

    [JsonIgnore]
    public string TranslatedName => Translator.Translate(Name);

    public int OverdueIn { get; set; }
    public string? HtmlColor { get; set; }
    public int MigrationNum { get; set; }
    public bool Default { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
    public string? DurationFormatted { get; set; }
    public string? Id2 { get; set; }
}
