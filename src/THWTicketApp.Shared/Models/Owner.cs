using System.Text.Json;
using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Owner
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    [JsonIgnore]
    public string? RoleName { get; set; }
    public JsonElement? Role
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                if (value.Value.ValueKind == JsonValueKind.String)
                    RoleName = value.Value.GetString();
                else if (value.Value.ValueKind == JsonValueKind.Object && value.Value.TryGetProperty("name", out var nameProp))
                    RoleName = nameProp.GetString();
            }
        }
    }
    public string? Title { get; set; }
}
