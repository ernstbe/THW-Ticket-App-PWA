namespace THWTicketApp.Shared.Models;

/// <summary>
/// Result of a successful ticket create.
/// <see cref="Id"/> is the Mongo _id (empty string when the server response
/// could not be parsed). <see cref="Uid"/> is trudesk's numeric ticket uid
/// (0 when missing/unparsable) — required by v2 follow-up endpoints such as
/// the ticket checklist, which address tickets by uid instead of _id.
/// </summary>
public sealed record TicketCreateResult(string Id, int Uid);
