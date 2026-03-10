using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Attachment
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("uploadDate")]
    public DateTime? UploadDate { get; set; }

    [JsonIgnore]
    public bool IsImage => MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    [JsonIgnore]
    public string SizeFormatted
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1} KB", Size / 1024.0);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1} MB", Size / (1024.0 * 1024.0));
        }
    }
}
