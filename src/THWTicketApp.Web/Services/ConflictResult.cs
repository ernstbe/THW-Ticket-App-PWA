using THWTicketApp.Shared.Data;

namespace THWTicketApp.Web.Services;

internal sealed record ConflictResult(ConflictType Type, string Reason);
