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
}
