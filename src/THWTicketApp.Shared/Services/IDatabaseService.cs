using THWTicketApp.Shared.Data;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Services;

public interface IDatabaseService
{
    Task<List<CachedTicket>> GetCachedTicketsAsync();
    Task<CachedTicket?> GetCachedTicketAsync(string id);
    Task SaveTicketsAsync(IEnumerable<Ticket> tickets);
    Task<List<Ticket>> GetTicketsFromCacheAsync();
    Task ClearCacheAsync();
    Task<DateTime?> GetLastCacheTimeAsync();
    Task<int> GetCachedTicketCountAsync();

    Task EnqueueActionAsync(string actionType, string payloadJson, DateTime? ticketUpdatedAt = null);
    Task MarkActionConflictedAsync(int id, string reason);
    Task<List<PendingAction>> GetPendingActionsAsync();
    Task<int> GetPendingActionCountAsync();
    Task RemoveActionAsync(int id);
    Task IncrementRetryCountAsync(int id);

    Task<bool> IsFavoriteAsync(string ticketId);
    Task ToggleFavoriteAsync(string ticketId);
    Task<HashSet<string>> GetFavoriteIdsAsync();

    Task<TimeEntry> StartTimerAsync(string ticketId);
    Task StopTimerAsync(int entryId, string? description = null);
    Task<TimeEntry?> GetActiveTimerAsync(string ticketId);
    Task<List<TimeEntry>> GetTimeEntriesAsync(string ticketId);
    Task<double> GetTotalTimeAsync(string ticketId);
    Task DeleteTimeEntryAsync(int entryId);

    Task AddLinkedTicketAsync(string sourceId, string linkedId, string linkedSubject, int linkedUid, string linkType = "related");
    Task<List<LinkedTicket>> GetLinkedTicketsAsync(string ticketId);
    Task RemoveLinkedTicketAsync(int linkId);

    Task AddNotificationAsync(string title, string description, string eventType, string ticketId);
    Task<List<NotificationEntry>> GetNotificationsAsync(int limit = 50);
    Task<int> GetUnreadNotificationCountAsync();
    Task MarkNotificationReadAsync(int id);
    Task MarkAllNotificationsReadAsync();
    Task ClearNotificationHistoryAsync();

    Task<int> SaveFilterAsync(SavedFilter filter);
    Task<List<SavedFilter>> GetSavedFiltersAsync();
    Task DeleteSavedFilterAsync(int id);
}
