using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

/// <summary>
/// One bidirectional link from a ticket to another (trudesk v2 feature).
/// <see cref="LinkType"/> is one of "related", "duplicate", "blocks" or the
/// server-generated inverse "blockedBy". Named to avoid clashing with the
/// legacy local-only <see cref="Data.LinkedTicket"/> cache entity.
/// </summary>
public class TicketLink
{
    public TicketLinkRef? Ticket { get; set; }
    public string? LinkType { get; set; }
}

/// <summary>
/// The populated target ticket of a <see cref="TicketLink"/>. The server only
/// selects uid, subject and status for the link list.
/// </summary>
public class TicketLinkRef
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public int Uid { get; set; }
    public string? Subject { get; set; }
    public Status? Status { get; set; }
}
