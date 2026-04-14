namespace THWTicketApp.Shared.Data;

public class PendingAction
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? TicketUpdatedAt { get; set; }
    public bool IsConflicted { get; set; }
    public string? ConflictReason { get; set; }
    public ConflictType ConflictType { get; set; } = ConflictType.None;
}
