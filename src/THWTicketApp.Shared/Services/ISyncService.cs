using THWTicketApp.Shared.Data;

namespace THWTicketApp.Shared.Services;

public interface ISyncService
{
    event Action<int>? PendingCountChanged;
    event Action<PendingAction>? ConflictDetected;

    Task<int> GetPendingCountAsync();
    Task EnqueueCommentAsync(string ticketId, int ticketUid, string ownerId, string comment, DateTime? ticketUpdatedAt = null);
    Task EnqueueNoteAsync(string ticketId, int ticketUid, string ownerId, string note, DateTime? ticketUpdatedAt = null);
    Task EnqueueCreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId);
    Task EnqueueAssignAsync(string ticketId, int ticketUid, string userId, DateTime? ticketUpdatedAt = null);
    Task<bool> SyncPendingActionsAsync();
    Task<bool> ForceApplyAsync(int actionId);
    Task DiscardActionAsync(int actionId);
    Task<List<PendingAction>> GetConflictedActionsAsync();
}
