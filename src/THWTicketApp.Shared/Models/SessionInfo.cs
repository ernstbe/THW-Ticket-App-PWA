namespace THWTicketApp.Shared.Models;

/// <summary>
/// One entry in the user's accessTokens array, as returned by
/// <c>GET /api/v1/account/sessions</c>. Token values themselves are
/// never sent to the client — only metadata. <see cref="IsLegacy"/> is
/// true for the legacy single-field token (pre PR #51); such entries
/// have no DeviceId and no timestamps because the legacy field never
/// captured them.
/// </summary>
public sealed class SessionInfo
{
    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsLegacy { get; set; }
}
