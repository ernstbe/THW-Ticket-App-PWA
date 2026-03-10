using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class TrudeskNotification
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public TrudeskNotificationData? Data { get; set; }
    public bool Unread { get; set; }
    public DateTime Date { get; set; }
}

public class TrudeskNotificationData
{
    public string? Ticket { get; set; }
    public int? TicketUid { get; set; }
}
