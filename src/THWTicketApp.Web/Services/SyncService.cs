using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using THWTicketApp.Shared.Data;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class SyncService : ISyncService
{
    public const int MaxAttachmentBytes = 5 * 1024 * 1024; // 5 MB

    // Retry backoff schedule — index = retryCount of the failed attempt (1-based).
    // After the 6th entry is exhausted (i.e. newRetryCount > 6), the action is dropped.
    internal static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    private readonly IIndexedDbService _indexedDb;
    private readonly ITrueDeskApiService _apiService;
    private readonly AppStateService _appState;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event Action<int>? PendingCountChanged;
    public event Action<PendingAction>? ConflictDetected;

    public SyncService(IIndexedDbService indexedDb, ITrueDeskApiService apiService, AppStateService appState)
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

    public Task EnqueueCommentAsync(string ticketId, int ticketUid, string ownerId, string comment, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "AddComment",
            TicketId = ticketId,
            TicketUid = ticketUid,
            OwnerId = ownerId,
            Content = comment,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueNoteAsync(string ticketId, int ticketUid, string ownerId, string note, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "AddNote",
            TicketId = ticketId,
            TicketUid = ticketUid,
            OwnerId = ownerId,
            Content = note,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueCreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "CreateTicket",
            Content = subject,
            Subject = subject,
            Issue = issue,
            TypeId = typeId,
            PriorityId = priorityId,
            GroupId = groupId,
            TargetUserId = assigneeId
        });

    public Task EnqueueAssignAsync(string ticketId, int ticketUid, string userId, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "AssignTicket",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TargetUserId = userId,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueClearAssigneeAsync(string ticketId, int ticketUid, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "ClearAssignee",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueStatusAsync(string ticketId, int ticketUid, string statusId, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "UpdateStatus",
            TicketId = ticketId,
            TicketUid = ticketUid,
            StatusId = statusId,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueUpdateTicketFieldsAsync(string ticketId, int ticketUid, string? subject, string? issue, string? priorityId, string? typeId, string? groupId, DateTime? dueDate, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "UpdateTicketFields",
            TicketId = ticketId,
            TicketUid = ticketUid,
            Subject = subject,
            Issue = issue,
            PriorityId = priorityId,
            TypeId = typeId,
            GroupId = groupId,
            DueDate = dueDate?.ToString("O"),
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueDeleteTicketAsync(string ticketId, int ticketUid, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "DeleteTicket",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueUploadAttachmentAsync(string ticketId, int ticketUid, string fileName, byte[] fileContent, string contentType, DateTime? ticketUpdatedAt = null)
    {
        if (fileContent == null) throw new ArgumentNullException(nameof(fileContent));
        if (fileContent.Length == 0) throw new ArgumentException("File content must not be empty.", nameof(fileContent));
        if (fileContent.Length > MaxAttachmentBytes)
            throw new ArgumentException($"Attachment exceeds {MaxAttachmentBytes / (1024 * 1024)} MB limit.", nameof(fileContent));

        return EnqueueAsync(new PendingActionDto
        {
            ActionType = "UploadAttachment",
            TicketId = ticketId,
            TicketUid = ticketUid,
            FileName = fileName,
            FileContentBase64 = Convert.ToBase64String(fileContent),
            FileContentType = contentType,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });
    }

    private async Task EnqueueAsync(PendingActionDto action)
    {
        action.CreatedAt = DateTime.UtcNow.ToString("O");
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action, JsonOptions));
        await UpdatePendingCount();
    }

    public async Task<bool> SyncPendingActionsAsync()
    {
        try
        {
            var json = await _indexedDb.GetPendingActionsAsync();
            var actions = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];

            var allSuccess = true;
            var now = DateTime.UtcNow;

            foreach (var action in actions)
            {
                // Skip already-conflicted actions (user must resolve them explicitly)
                if (action.IsConflicted)
                    continue;

                // Respect backoff schedule
                if (!string.IsNullOrEmpty(action.NextRetryAt)
                    && TryParseUtc(action.NextRetryAt, out var nextRetry)
                    && nextRetry > now)
                {
                    allSuccess = false;
                    continue;
                }

                try
                {
                    // Conflict detection for actions targeting an existing ticket
                    if (NeedsConflictCheck(action))
                    {
                        var conflict = await CheckConflictAsync(action);
                        if (conflict is { } c && c.Type != ConflictType.None)
                        {
                            await _indexedDb.MarkActionConflictedAsync(action.Id, c.Reason, c.Type.ToString());
                            var pa = ToPendingAction(action);
                            pa.IsConflicted = true;
                            pa.ConflictReason = c.Reason;
                            pa.ConflictType = c.Type;
                            ConflictDetected?.Invoke(pa);
                            await AppendLogAsync("warn", action, $"Conflict ({c.Type}): {c.Reason}");
                            allSuccess = false;
                            continue;
                        }
                    }

                    var success = await ApplyActionAsync(action);

                    if (success)
                    {
                        if (action.Id > 0)
                            await _indexedDb.RemovePendingActionAsync(action.Id);
                        await AppendLogAsync("info", action, "Synced successfully");
                    }
                    else
                    {
                        allSuccess = false;
                        await HandleFailureAsync(action, "API call returned failure", null);
                    }
                }
                catch (Exception ex)
                {
                    allSuccess = false;
                    await HandleFailureAsync(action, ex.Message, ex);
                }
            }

            await UpdatePendingCount();
            return allSuccess;
        }
        catch (Exception ex)
        {
            await AppendLogAsync("error", null, "Sync loop crashed: " + ex.Message, ex);
            return false;
        }
    }

    public async Task<bool> ForceApplyAsync(int actionId)
    {
        try
        {
            var json = await _indexedDb.GetConflictedActionsAsync();
            var actions = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];
            var action = actions.FirstOrDefault(a => a.Id == actionId);
            if (action == null) return false;

            var success = await ApplyActionAsync(action);

            if (success)
            {
                await _indexedDb.RemovePendingActionAsync(actionId);
                await UpdatePendingCount();
                await AppendLogAsync("info", action, "Force-applied after conflict");
            }
            return success;
        }
        catch (Exception ex)
        {
            await AppendLogAsync("error", null, $"ForceApply crashed for action {actionId}: {ex.Message}", ex);
            return false;
        }
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

    private async Task<bool> ApplyActionAsync(PendingActionDto action) => action.ActionType switch
    {
        "AddComment" => await _apiService.AddCommentAsync(action.TicketId!, action.OwnerId!, action.Content!),
        "AddNote" => await _apiService.AddNoteAsync(action.TicketId!, action.OwnerId!, action.Content!),
        "AssignTicket" => await _apiService.AssignTicketAsync(action.TicketId!, action.TargetUserId!),
        "ClearAssignee" => await _apiService.ClearTicketAssigneeAsync(action.TicketId!),
        "UpdateStatus" => await _apiService.UpdateTicketStatusAsync(action.TicketId!, action.StatusId!),
        "CreateTicket" => await _apiService.CreateTicketAsync(
            action.Subject ?? action.Content ?? "", action.Issue, action.TypeId, action.PriorityId, action.GroupId, action.TargetUserId),
        "UpdateTicketFields" => await ApplyUpdateTicketFieldsAsync(action),
        "DeleteTicket" => await _apiService.DeleteTicketAsync(action.TicketId!),
        "UploadAttachment" => await ApplyUploadAttachmentAsync(action),
        _ => false
    };

    private Task<bool> ApplyUpdateTicketFieldsAsync(PendingActionDto action)
    {
        var ticket = new Ticket
        {
            Id = action.TicketId ?? string.Empty,
            Uid = action.TicketUid,
            Subject = action.Subject,
            Issue = action.Issue
        };
        if (!string.IsNullOrEmpty(action.PriorityId))
            ticket.Priority = new Priority { Id = action.PriorityId };
        if (!string.IsNullOrEmpty(action.TypeId))
            ticket.Type = new TicketType { Id = action.TypeId };
        if (!string.IsNullOrEmpty(action.GroupId))
            ticket.Group = new Group { Id = action.GroupId };
        if (!string.IsNullOrEmpty(action.DueDate) && DateTime.TryParse(action.DueDate, out var dd))
            ticket.DueDate = dd;

        return _apiService.EditTicketAsync(ticket);
    }

    private async Task<bool> ApplyUploadAttachmentAsync(PendingActionDto action)
    {
        if (string.IsNullOrEmpty(action.TicketId) || string.IsNullOrEmpty(action.FileContentBase64) || string.IsNullOrEmpty(action.FileName))
            return false;

        var bytes = Convert.FromBase64String(action.FileContentBase64);
        using var stream = new MemoryStream(bytes);
        return await _apiService.UploadAttachmentAsync(action.TicketId, stream, action.FileName);
    }

    private static bool NeedsConflictCheck(PendingActionDto action)
    {
        // Actions that target an existing ticket's state need conflict checks.
        // CreateTicket has no pre-existing ticket; UploadAttachment intentionally
        // bypasses the updated-field check since attachments shouldn't block on
        // unrelated edits. DeleteTicket DOES check — if the ticket is already
        // gone, we want to surface that as TicketDeleted rather than silently
        // retrying and failing.
        if (action.ActionType is "CreateTicket" or "UploadAttachment")
            return false;
        return !string.IsNullOrEmpty(action.TicketId) && action.TicketUid > 0;
    }

    internal async Task<ConflictResult?> CheckConflictAsync(PendingActionDto action)
    {
        if (action.TicketUid <= 0)
            return null;

        (int statusCode, string body) response;
        try
        {
            response = await _apiService.GetTicketRawAsync(action.TicketUid.ToString());
        }
        catch
        {
            // Network error — allow the normal retry loop to handle it.
            return null;
        }

        // 404 — ticket was deleted server-side since the action was queued.
        if (response.statusCode == 404)
        {
            if (action.ActionType == "DeleteTicket")
                return null; // our action's goal was achieved by someone else
            return new ConflictResult(
                ConflictType.TicketDeleted,
                "Das Ticket wurde serverseitig gelöscht, seit die Aktion erstellt wurde.");
        }

        // 401/403 — the user lost access to the ticket.
        if (response.statusCode is 401 or 403)
        {
            return new ConflictResult(
                ConflictType.PermissionRevoked,
                "Keine Berechtigung mehr für dieses Ticket.");
        }

        // Non-success for any other reason — treat as transient, let retry handle it.
        if (response.statusCode < 200 || response.statusCode >= 300)
            return null;

        // Happy path: parse body and look for update/status drift.
        try
        {
            using var doc = JsonDocument.Parse(response.body);

            // Navigate to ticket data (handles both v1 and v2 response formats)
            JsonElement ticketEl;
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                ticketEl = dataEl;
            else if (doc.RootElement.TryGetProperty("ticket", out var tEl))
                ticketEl = tEl;
            else
                ticketEl = doc.RootElement;

            // No expected baseline to compare against — skip.
            if (string.IsNullOrEmpty(action.TicketUpdatedAt))
                return null;

            if (!ticketEl.TryGetProperty("updated", out var updatedEl))
                return null;

            var serverUpdated = updatedEl.GetString();
            if (string.IsNullOrEmpty(serverUpdated) || serverUpdated == action.TicketUpdatedAt)
                return null;

            // Ticket has drifted. Is it specifically a status change while our
            // action is an UpdateStatus targeting a different status?
            if (action.ActionType == "UpdateStatus"
                && !string.IsNullOrEmpty(action.StatusId)
                && ticketEl.TryGetProperty("status", out var statusEl))
            {
                var serverStatusId = ExtractStatusId(statusEl);
                if (!string.IsNullOrEmpty(serverStatusId) && serverStatusId != action.StatusId)
                {
                    return new ConflictResult(
                        ConflictType.StatusChanged,
                        $"Der Status wurde bereits auf einen anderen Wert geändert.");
                }
            }

            var updatedTime = TryParseUtc(serverUpdated, out var dt)
                ? $"vor {FormatTimeAgo(dt)}" : "kürzlich";
            return new ConflictResult(
                ConflictType.TicketUpdated,
                $"Ticket wurde {updatedTime} geändert (seitdem die Aktion erstellt wurde)");
        }
        catch
        {
            // Body isn't parseable — can't confirm a conflict, let the apply step try.
            return null;
        }
    }

    private static string? ExtractStatusId(JsonElement statusEl)
    {
        // trudesk returns status as either a populated object {_id, name, ...}
        // or the raw id string, depending on the endpoint version.
        if (statusEl.ValueKind == JsonValueKind.String)
            return statusEl.GetString();
        if (statusEl.ValueKind == JsonValueKind.Object)
        {
            if (statusEl.TryGetProperty("_id", out var idEl)) return idEl.GetString();
            if (statusEl.TryGetProperty("id", out var idEl2)) return idEl2.GetString();
        }
        return null;
    }

    private async Task HandleFailureAsync(PendingActionDto action, string errorMessage, Exception? exception)
    {
        if (action.Id <= 0)
            return;

        var newRetryCount = action.RetryCount + 1;

        if (newRetryCount > RetryDelays.Length)
        {
            // Exhausted all retries — drop the action and record a permanent failure.
            await _indexedDb.RemovePendingActionAsync(action.Id);
            await AppendLogAsync("error", action, $"Gave up after {newRetryCount} failed attempts: {errorMessage}", exception);
            return;
        }

        var delay = RetryDelays[newRetryCount - 1];
        var nextRetry = DateTime.UtcNow + delay;
        await _indexedDb.UpdateRetryStateAsync(action.Id, nextRetry.ToString("O"), newRetryCount, errorMessage);
        await AppendLogAsync("warn", action, $"Attempt {newRetryCount} failed, retrying in {FormatDelay(delay)}: {errorMessage}", exception);
    }

    private async Task AppendLogAsync(string level, PendingActionDto? action, string message, Exception? exception = null)
    {
        try
        {
            var entry = new SyncLogEntry
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                Level = level,
                ActionId = action?.Id,
                ActionType = action?.ActionType,
                TicketId = action?.TicketId,
                Message = message,
                Exception = exception?.ToString()
            };
            await _indexedDb.AppendSyncLogAsync(JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch
        {
            // Logging must never throw back into the sync loop.
        }
    }

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc))
            return true;
        utc = default;
        return false;
    }

    private static string FormatDelay(TimeSpan delay)
    {
        if (delay.TotalMinutes >= 1) return $"{(int)delay.TotalMinutes}min";
        return $"{(int)delay.TotalSeconds}s";
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
        PayloadJson = JsonSerializer.Serialize(dto, JsonOptions),
        CreatedAt = DateTime.TryParse(dto.CreatedAt, out var dt) ? dt : DateTime.UtcNow,
        RetryCount = dto.RetryCount,
        TicketUpdatedAt = DateTime.TryParse(dto.TicketUpdatedAt, out var tdt) ? tdt : null,
        IsConflicted = dto.IsConflicted,
        ConflictReason = dto.ConflictReason,
        ConflictType = Enum.TryParse<ConflictType>(dto.ConflictType, out var ct) ? ct : ConflictType.None
    };

    private async Task UpdatePendingCount()
    {
        _appState.PendingActionsCount = await _indexedDb.GetPendingActionCountAsync();
        _appState.NotifyStateChanged();
        PendingCountChanged?.Invoke(_appState.PendingActionsCount);
    }

    private sealed class SyncLogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int? ActionId { get; set; }
        public string? ActionType { get; set; }
        public string? TicketId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }
}
