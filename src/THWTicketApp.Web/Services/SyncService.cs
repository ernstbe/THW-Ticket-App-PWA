using System.Text.Json;
using THWTicketApp.Shared.Data;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class SyncService : ISyncService
{
    private readonly IndexedDbService _indexedDb;
    private readonly ITrueDeskApiService _apiService;
    private readonly AppStateService _appState;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public event Action<int>? PendingCountChanged;
    public event Action<PendingAction>? ConflictDetected;

    public SyncService(IndexedDbService indexedDb, ITrueDeskApiService apiService, AppStateService appState)
    {
        _indexedDb = indexedDb;
        _apiService = apiService;
        _appState = appState;
    }

    // Cache helpers (not part of ISyncService but used by pages)
    public async Task CacheTicketsAsync(string ticketsJson)
    {
        await _indexedDb.SaveTicketsAsync(ticketsJson);
    }

    public async Task<string> GetCachedTicketsAsync()
    {
        return await _indexedDb.GetTicketsAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _indexedDb.GetPendingActionCountAsync();
    }

    public async Task EnqueueCommentAsync(string ticketId, int ticketUid, string ownerId, string comment, DateTime? ticketUpdatedAt = null)
    {
        var action = new PendingActionDto
        {
            ActionType = "AddComment",
            TicketId = ticketId,
            TicketUid = ticketUid,
            OwnerId = ownerId,
            Content = comment,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action));
        await UpdatePendingCount();
    }

    public async Task EnqueueNoteAsync(string ticketId, int ticketUid, string ownerId, string note, DateTime? ticketUpdatedAt = null)
    {
        var action = new PendingActionDto
        {
            ActionType = "AddNote",
            TicketId = ticketId,
            TicketUid = ticketUid,
            OwnerId = ownerId,
            Content = note,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action));
        await UpdatePendingCount();
    }

    public async Task EnqueueCreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId)
    {
        var action = new PendingActionDto
        {
            ActionType = "CreateTicket",
            Content = subject,
            Subject = subject,
            Issue = issue,
            TypeId = typeId,
            PriorityId = priorityId,
            GroupId = groupId,
            TargetUserId = assigneeId,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action));
        await UpdatePendingCount();
    }

    public async Task EnqueueAssignAsync(string ticketId, int ticketUid, string userId, DateTime? ticketUpdatedAt = null)
    {
        var action = new PendingActionDto
        {
            ActionType = "AssignTicket",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TargetUserId = userId,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action));
        await UpdatePendingCount();
    }

    public async Task<bool> SyncPendingActionsAsync()
    {
        try
        {
            var json = await _indexedDb.GetPendingActionsAsync();
            var actions = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];

            var allSuccess = true;
            foreach (var action in actions)
            {
                // Skip already-conflicted actions
                if (action.IsConflicted)
                    continue;

                try
                {
                    // Check for conflicts (except for CreateTicket which has no existing ticket)
                    if (action.ActionType != "CreateTicket" && !string.IsNullOrEmpty(action.TicketId)
                        && !string.IsNullOrEmpty(action.TicketUpdatedAt))
                    {
                        var conflict = await CheckConflictAsync(action);
                        if (conflict != null)
                        {
                            await _indexedDb.MarkActionConflictedAsync(action.Id, conflict);
                            var pa = ToPendingAction(action);
                            pa.IsConflicted = true;
                            pa.ConflictReason = conflict;
                            ConflictDetected?.Invoke(pa);
                            allSuccess = false;
                            continue;
                        }
                    }

                    var success = action.ActionType switch
                    {
                        "AddComment" => await _apiService.AddCommentAsync(action.TicketId!, action.OwnerId!, action.Content!),
                        "AddNote" => await _apiService.AddNoteAsync(action.TicketId!, action.OwnerId!, action.Content!),
                        "AssignTicket" => await _apiService.AssignTicketAsync(action.TicketId!, action.TargetUserId!),
                        "UpdateStatus" => await _apiService.UpdateTicketStatusAsync(action.TicketId!, action.StatusId!),
                        "CreateTicket" => await _apiService.CreateTicketAsync(
                            action.Subject ?? action.Content ?? "", action.Issue, action.TypeId, action.PriorityId, action.GroupId, action.TargetUserId),
                        "ClearAssignee" => await _apiService.ClearTicketAssigneeAsync(action.TicketId!),
                        _ => false
                    };

                    if (success && action.Id > 0)
                    {
                        await _indexedDb.RemovePendingActionAsync(action.Id);
                    }
                    else if (!success)
                    {
                        allSuccess = false;
                        var retryCount = await _indexedDb.IncrementRetryCountAsync(action.Id);
                        if (retryCount >= 5)
                            await _indexedDb.RemovePendingActionAsync(action.Id);
                    }
                }
                catch
                {
                    allSuccess = false;
                    if (action.Id > 0)
                        await _indexedDb.IncrementRetryCountAsync(action.Id);
                }
            }

            await UpdatePendingCount();
            return allSuccess;
        }
        catch { return false; }
    }

    public async Task<bool> ForceApplyAsync(int actionId)
    {
        try
        {
            var json = await _indexedDb.GetConflictedActionsAsync();
            var actions = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];
            var action = actions.FirstOrDefault(a => a.Id == actionId);
            if (action == null) return false;

            var success = action.ActionType switch
            {
                "AddComment" => await _apiService.AddCommentAsync(action.TicketId!, action.OwnerId!, action.Content!),
                "AddNote" => await _apiService.AddNoteAsync(action.TicketId!, action.OwnerId!, action.Content!),
                "AssignTicket" => await _apiService.AssignTicketAsync(action.TicketId!, action.TargetUserId!),
                "UpdateStatus" => await _apiService.UpdateTicketStatusAsync(action.TicketId!, action.StatusId!),
                "CreateTicket" => await _apiService.CreateTicketAsync(
                    action.Subject ?? action.Content ?? "", action.Issue, action.TypeId, action.PriorityId, action.GroupId, action.TargetUserId),
                "ClearAssignee" => await _apiService.ClearTicketAssigneeAsync(action.TicketId!),
                _ => false
            };

            if (success)
            {
                await _indexedDb.RemovePendingActionAsync(actionId);
                await UpdatePendingCount();
            }
            return success;
        }
        catch { return false; }
    }

    public async Task DiscardActionAsync(int actionId)
    {
        await _indexedDb.RemovePendingActionAsync(actionId);
        await UpdatePendingCount();
    }

    public async Task<List<PendingAction>> GetConflictedActionsAsync()
    {
        var json = await _indexedDb.GetConflictedActionsAsync();
        var dtos = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];
        return dtos.Select(ToPendingAction).ToList();
    }

    private async Task<string?> CheckConflictAsync(PendingActionDto action)
    {
        if (string.IsNullOrEmpty(action.TicketUpdatedAt) || action.TicketUid <= 0)
            return null;

        try
        {
            var ticketJson = await _apiService.GetTicketAsync(action.TicketUid.ToString());
            using var doc = JsonDocument.Parse(ticketJson);

            // Navigate to ticket data (handles both v1 and v2 response formats)
            JsonElement ticketEl;
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                ticketEl = dataEl;
            else if (doc.RootElement.TryGetProperty("ticket", out var tEl))
                ticketEl = tEl;
            else
                ticketEl = doc.RootElement;

            if (ticketEl.TryGetProperty("updated", out var updatedEl))
            {
                var serverUpdated = updatedEl.GetString();
                if (!string.IsNullOrEmpty(serverUpdated) && serverUpdated != action.TicketUpdatedAt)
                {
                    // Ticket was modified since the action was queued
                    var updatedTime = DateTime.TryParse(serverUpdated, out var dt)
                        ? $"vor {FormatTimeAgo(dt)}" : "kürzlich";
                    return $"Ticket wurde {updatedTime} geändert (seitdem die Aktion erstellt wurde)";
                }
            }
        }
        catch
        {
            // Cannot check - allow the action to proceed
        }
        return null;
    }

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "wenigen Sekunden";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} Minuten";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} Stunden";
        return $"{(int)diff.TotalDays} Tagen";
    }

    private static PendingAction ToPendingAction(PendingActionDto dto) => new()
    {
        Id = dto.Id,
        ActionType = dto.ActionType,
        PayloadJson = JsonSerializer.Serialize(dto),
        CreatedAt = DateTime.TryParse(dto.CreatedAt, out var dt) ? dt : DateTime.UtcNow,
        RetryCount = dto.RetryCount,
        TicketUpdatedAt = DateTime.TryParse(dto.TicketUpdatedAt, out var tdt) ? tdt : null,
        IsConflicted = dto.IsConflicted,
        ConflictReason = dto.ConflictReason
    };

    private async Task UpdatePendingCount()
    {
        _appState.PendingActionsCount = await _indexedDb.GetPendingActionCountAsync();
        _appState.NotifyStateChanged();
        PendingCountChanged?.Invoke(_appState.PendingActionsCount);
    }

    private class PendingActionDto
    {
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
        public string? TicketUpdatedAt { get; set; }
        public string? CreatedAt { get; set; }
        public int RetryCount { get; set; }
        public bool IsConflicted { get; set; }
        public string? ConflictReason { get; set; }
    }
}
