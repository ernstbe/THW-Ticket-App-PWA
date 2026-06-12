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
    Task<bool> TryUnlockSessionAsync();
    Task LogoutAsync();

    /// <summary>
    /// Fires BEFORE LogoutAsync clears tokens or calls /logout. Listeners can
    /// still make authenticated requests — used to e.g. unregister web-push
    /// subscriptions server-side while we still have a token. Awaited;
    /// exceptions from handlers are swallowed so a flaky listener can't
    /// break logout itself.
    /// </summary>
    event Func<Task>? LoggingOut;

    Task<string> GetTicketsAsync();
    Task<string> GetTicketsPagedAsync(int page = 0, int limit = 50);
    Task<string> GetTicketsFilteredAsync(string? status = null, bool? assignedSelf = null, int limit = 1000);
    Task<string> SearchTicketsAsync(string query);
    Task<string> GetTicketAsync(string ticketUid);
    Task<(int StatusCode, string Body)> GetTicketRawAsync(string ticketUid);
    Task<string> AddTicketAsync(string title, string description, string? assigneeId);
    /// <summary>
    /// Creates a ticket and returns its id + uid on success, or null on
    /// failure. Used by AddTicket to chain follow-up calls (attachment
    /// uploads via _id) from the same form submission. Offline queue
    /// replay treats any non-null result as success. On parse trouble the
    /// create still counts as success and returns an empty Id / zero Uid —
    /// callers needing either value must check explicitly.
    /// `checklist` titles ride along in the create payload (trudesk
    /// validates `checklist` on the ticketsV2 create and stores the items
    /// with completed:false).
    /// </summary>
    Task<TicketCreateResult?> CreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId, DateTime? dueDate = null, IReadOnlyList<string>? checklist = null);
    /// <summary>
    /// Updates ticket fields. When <paramref name="includeDueDate"/> is true
    /// (default), dueDate is always sent — MinValue goes out as an explicit
    /// null so the server clears the date. Pass false for partial updates
    /// that must leave the server-side due date untouched.
    /// </summary>
    Task<bool> EditTicketAsync(Ticket ticket, bool includeDueDate = true);
    Task<bool> DeleteTicketAsync(string ticketId);
    Task<bool> UpdateTicketStatusAsync(string ticketId, string statusId);

    Task<bool> AssignTicketAsync(string ticketId, string userId);
    Task<bool> ClearTicketAssigneeAsync(string ticketId);
    /// <summary>
    /// Replaces the whole additionalAssignees array of a ticket. An empty
    /// list clears it. The server de-duplicates and drops the primary
    /// assignee id, so callers don't need to pre-filter.
    /// </summary>
    Task<bool> SetAdditionalAssigneesAsync(string ticketId, IEnumerable<string> userIds);

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
    Task<bool> MarkNotificationReadAsync(string notificationId);
    Task<int> MarkAllNotificationsReadAsync();

    Task<string> GetTicketStatsAsync(int timespan = 30);
    Task<string> GetTicketStatsForGroupAsync(string groupId);
    Task<string> GetTicketStatsForUserAsync(string userId);
    Task<string> GetTicketStatsForAssigneeAsync(string userId);

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
    Task<UserProfile?> GetCurrentUserProfileAsync();
    Task<bool> UpdateProfileAsync(string fullname, string? title, string? workNumber, string? mobileNumber);

    Task<List<SessionInfo>> GetSessionsAsync();
    Task<bool> RevokeSessionAsync(string deviceId);
    Task<bool> RevokeAllOtherSessionsAsync();
    Task<bool> UpdatePasswordAsync(string currentPassword, string newPassword, string confirmPassword);

    // Web Push (v1)
    Task<string?> GetWebPushVapidPublicKeyAsync();
    Task<bool> SubscribeWebPushAsync(string endpoint, string p256dh, string auth, string? deviceId, string? userAgent);
    Task<bool> UnsubscribeWebPushAsync(string endpoint);

    // Bug reports (v1)
    Task<bool> SubmitBugReportAsync(string title, string? description, Dictionary<string, object?>? context);
    Task<List<BugReport>> ListBugReportsAsync();
    Task<bool> SetBugReportResolvedAsync(string id, bool resolved);
    Task<bool> DeleteBugReportAsync(string id);

    /// <summary>
    /// True when the logged-in user has the trudesk "admin" role
    /// (matched via the populated `role.normalized` field on `/api/v1/login`).
    /// Result is cached per session — clears on logout.
    /// </summary>
    Task<bool> IsCurrentUserAdminAsync();

    // Public registration (v1)
    Task<string?> GetCaptchaSvgAsync();
    Task<(bool Success, bool Exists, string? Error)> CheckEmailAsync(string email, string captcha);
    Task<(bool Success, string? Error)> RegisterAsync(string username, string fullname, string email, string password, string captcha);
}
