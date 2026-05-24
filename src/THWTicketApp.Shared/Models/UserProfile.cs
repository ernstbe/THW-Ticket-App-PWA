namespace THWTicketApp.Shared.Models;

/// <summary>
/// Subset of the trudesk User document returned by GET /api/v2/login.
/// Used to populate the Profile page on load — the existing User class
/// is shared with assignee/comment-owner usage and carries more fields
/// than we need here.
/// </summary>
public sealed class UserProfile
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public string? Title { get; set; }
    public string? WorkNumber { get; set; }
    public string? MobileNumber { get; set; }
    /// <summary>
    /// Group ids visible to this user, as resolved server-side by
    /// trudesk's <c>accountsApi.sessionUser</c>: for admins/agents the
    /// Team → Department → Groups chain; for customers the direct
    /// Group memberships (Group.members). Use this — not the public
    /// <c>/api/v1/groups</c> response — when deciding whether a per-group
    /// filter UI is meaningful for the current user. May be empty.
    /// </summary>
    public List<string> Groups { get; set; } = new();
}
