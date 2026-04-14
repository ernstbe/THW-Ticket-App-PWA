namespace THWTicketApp.Web.Services;

public interface IIndexedDbService
{
    Task SaveTicketsAsync(string ticketsJson);
    Task<string> GetTicketsAsync();
    Task<int> GetCachedTicketCountAsync();
    Task ClearTicketCacheAsync();

    Task EnqueuePendingActionAsync(string actionJson);
    Task<string> GetPendingActionsAsync();
    Task<int> GetPendingActionCountAsync();
    Task RemovePendingActionAsync(int id);
    Task ClearPendingActionsAsync();

    Task<bool> MarkActionConflictedAsync(int id, string reason, string conflictType);
    Task<string> GetConflictedActionsAsync();

    Task<bool> UpdateRetryStateAsync(int id, string nextRetryAtIso, int retryCount, string? errorMessage);

    Task AppendSyncLogAsync(string entryJson);
    Task<string> GetSyncLogAsync(int limit = 0);
    Task ClearSyncLogAsync();

    Task<string?> GetLastCacheTimeAsync();
}
