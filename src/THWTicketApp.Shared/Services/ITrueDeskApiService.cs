using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Services;

public interface ITrueDeskApiService
{
    string? CurrentUsername { get; }
    string? CurrentUserId { get; }
    string? LastError { get; }
    bool IsAuthenticated { get; }

    Task<bool> AuthenticateAsync(string username, string password);
    Task<bool> TryRestoreSessionAsync();
    Task LogoutAsync();

    Task<string> GetTicketsAsync();
    Task<string> GetTicketsPagedAsync(int page = 0, int limit = 50);
    Task<string> GetTicketsFilteredAsync(string? status = null, bool? assignedSelf = null, int limit = 1000);
    Task<string> SearchTicketsAsync(string query);
    Task<string> GetTicketAsync(string ticketUid);
    Task<(int StatusCode, string Body)> GetTicketRawAsync(string ticketUid);
    Task<string> AddTicketAsync(string title, string description, string? assigneeId);
    Task<bool> CreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId);
    Task<bool> EditTicketAsync(Ticket ticket);
    Task<bool> DeleteTicketAsync(string ticketId);
    Task<bool> UpdateTicketStatusAsync(string ticketId, string statusId);

    Task<bool> AssignTicketAsync(string ticketId, string userId);
    Task<bool> ClearTicketAssigneeAsync(string ticketId);

    Task<bool> AddCommentAsync(string id, string ownerId, string newComment);
    Task<bool> AddNoteAsync(string ticketId, string ownerId, string note);

    Task<bool> UploadAttachmentAsync(string ticketId, Stream fileStream, string fileName);
    Task<Stream?> DownloadAttachmentAsync(string attachmentPath);
    string GetAttachmentUrl(string attachmentPath);
    Task<bool> DeleteAttachmentAsync(string ticketId, string attachmentId);

    Task<string> GetStatusesAsync();
    Task<string> GetUsersAsync();
    Task<string> GetAssigneesAsync();
    Task<string> GetTicketTypesAsync();
    Task<string> GetPrioritiesAsync();
    Task<string> GetTagsAsync();
    Task<string> GetGroupsAsync();

    Task<string> GetTicketsByGroupAsync(string groupId, int page = 0, int limit = 50);
    Task<string> GetOverdueTicketsAsync();
    Task<bool> SubscribeToTicketAsync(string ticketId, bool subscribe);

    Task<string> GetNotificationsAsync();
    Task<int> GetNotificationCountAsync();

    Task<string> GetTicketStatsAsync(int timespan = 30);
    Task<string> GetTicketStatsForGroupAsync(string groupId);
    Task<string> GetTicketStatsForUserAsync(string userId);

    // Recurring Tasks (v2)
    Task<string> GetRecurringTasksAsync();
    Task<string> GetRecurringTaskAsync(string taskId);
    Task<bool> CreateRecurringTaskAsync(Dictionary<string, object?> taskData);
    Task<bool> UpdateRecurringTaskAsync(string taskId, Dictionary<string, object?> taskData);
    Task<bool> DeleteRecurringTaskAsync(string taskId);

    // Assets (v2)
    Task<string> GetAssetsAsync();
    Task<string> GetAssetAsync(string assetId);
    Task<bool> CreateAssetAsync(Dictionary<string, object?> assetData);
    Task<bool> UpdateAssetAsync(string assetId, Dictionary<string, object?> assetData);
    Task<bool> DeleteAssetAsync(string assetId);
    Task<bool> LinkAssetToTicketAsync(string assetId, string ticketUid);

    // Reports (v2)
    Task<string> GetHandoverReportAsync(string format = "json");
    Task<string> GetSitzungReportAsync(string format = "json");
}
