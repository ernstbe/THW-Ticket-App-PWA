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

    Task<bool> AddCommentAsync(string ticketUid, string ownerId, string newComment);
    Task<bool> AddNoteAsync(string ticketUid, string ownerId, string note);

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
    Task<bool> SubscribeToTicketAsync(string ticketUid, bool subscribe);

    Task<string> GetNotificationsAsync();
    Task<int> GetNotificationCountAsync();

    Task<string> GetTicketStatsAsync(int timespan = 30);
    Task<string> GetTicketStatsForGroupAsync(string groupId);
    Task<string> GetTicketStatsForUserAsync(string userId);

    // Dashboard (v2)
    Task<string> GetDashboardWidgetsAsync();

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

    // Teams (v2)
    Task<string> GetTeamsAsync();
    Task<string> GetTeamAsync(string teamId);
    Task<bool> CreateTeamAsync(Dictionary<string, object?> teamData);
    Task<bool> UpdateTeamAsync(string teamId, Dictionary<string, object?> teamData);
    Task<bool> DeleteTeamAsync(string teamId);

    // Departments (v2)
    Task<string> GetDepartmentsAsync();
    Task<string> GetDepartmentAsync(string departmentId);
    Task<bool> CreateDepartmentAsync(Dictionary<string, object?> departmentData);
    Task<bool> UpdateDepartmentAsync(string departmentId, Dictionary<string, object?> departmentData);
    Task<bool> DeleteDepartmentAsync(string departmentId);

    // Ticket Templates (v2)
    Task<string> GetTicketTemplatesAsync();
    Task<string> GetTicketTemplateAsync(string templateId);
    Task<bool> CreateTicketTemplateAsync(Dictionary<string, object?> templateData);
    Task<bool> UpdateTicketTemplateAsync(string templateId, Dictionary<string, object?> templateData);
    Task<bool> DeleteTicketTemplateAsync(string templateId);

    // Calendar (v2)
    Task<string> GetCalendarEventsAsync(DateTime start, DateTime end);

    // Documents (v2)
    Task<string> GetDocumentsAsync();
    Task<bool> CreateDocumentAsync(string name, string? description, string? category);
    Task<bool> DeleteDocumentAsync(string documentId);

    // Notices (v2)
    Task<string> GetNoticesAsync();
    Task<bool> CreateNoticeAsync(string name, string message, string color, string fontColor);
    Task<bool> ActivateNoticeAsync(string noticeId);
    Task<bool> ClearNoticesAsync();
    Task<bool> DeleteNoticeAsync(string noticeId);

    // Ticket tags — implemented on top of the existing ticket PUT endpoint
    Task<bool> UpdateTicketTagsAsync(string ticketId, IEnumerable<string> tagIds);
    Task<bool> AddTagToTicketAsync(string ticketId, string tagId);
    Task<bool> RemoveTagFromTicketAsync(string ticketId, string tagId);

    // Ticket checklist (v2)
    Task<bool> AddChecklistItemAsync(string ticketUid, string title);
    Task<bool> UpdateChecklistItemAsync(string ticketUid, string itemId, string? title = null, bool? completed = null);
    Task<bool> DeleteChecklistItemAsync(string ticketUid, string itemId);

    // Batch operations (v2)
    Task<(int Deleted, int Failed)> BatchDeleteTicketsAsync(IEnumerable<string> ticketIds);
    Task<(int Updated, int Failed)> BatchUpdateTicketsAsync(IEnumerable<Dictionary<string, object?>> batch);

    // Profile (v2)
    Task<bool> UpdateProfileAsync(string fullname, string? title, string? workNumber, string? mobileNumber);
    Task<bool> UpdatePasswordAsync(string currentPassword, string newPassword, string confirmPassword);

    // Public registration (v1)
    Task<string?> GetCaptchaSvgAsync();
    Task<(bool Success, bool Exists, string? Error)> CheckEmailAsync(string email, string captcha);
    Task<(bool Success, string? Error)> RegisterAsync(string username, string fullname, string email, string password, string captcha);
}
