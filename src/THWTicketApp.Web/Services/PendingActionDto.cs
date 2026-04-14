using System.Text.Json.Serialization;

namespace THWTicketApp.Web.Services;

internal sealed class PendingActionDto
{
    // Skip Id when it's 0 (pre-insert) so IndexedDB autoIncrement assigns one.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Id { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string? TicketId { get; set; }
    public int TicketUid { get; set; }
    public string? OwnerId { get; set; }
    public string? Content { get; set; }
    public string? Subject { get; set; }
    public string? Issue { get; set; }
    public string? TypeId { get; set; }
    public string? PriorityId { get; set; }
    public string? GroupId { get; set; }
    public string? TargetUserId { get; set; }
    public string? StatusId { get; set; }
    public string? DueDate { get; set; }
    public string? FileName { get; set; }
    public string? FileContentBase64 { get; set; }
    public string? FileContentType { get; set; }
    public string? TicketUpdatedAt { get; set; }
    public string? CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public string? NextRetryAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool IsConflicted { get; set; }
    public string? ConflictReason { get; set; }
    public string? ConflictType { get; set; }
}
