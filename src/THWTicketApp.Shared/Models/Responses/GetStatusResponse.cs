using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models.Responses;

public class GetStatusResponse
{
    public bool Success { get; set; }
    [JsonPropertyName("status")]
    public List<Status> Statuses { get; set; } = [];
}
