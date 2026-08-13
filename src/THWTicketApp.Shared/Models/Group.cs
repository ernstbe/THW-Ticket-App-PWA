using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Group
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool Public { get; set; }
    // #privatetickets — hidden single-member group auto-created per user via
    // the Settings "private ticket space" toggle. Never show the raw Name
    // (a "private:<objectid>" string) — use GroupDisplayHelper.GetDisplayName.
    public bool Private { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
}
