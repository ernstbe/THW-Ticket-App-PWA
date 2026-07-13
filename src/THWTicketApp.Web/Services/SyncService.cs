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

    // Offline read-cache helpers (used by the ticket list / dashboard / kanban).
    public async Task CacheTicketsAsync(string ticketsJson)
    {
        await _indexedDb.SaveTicketsAsync(ticketsJson);
    }

    public async Task<string> GetCachedTicketsAsync()
    {
        return await _indexedDb.GetTicketsAsync();
    }

    public Task<string?> GetLastCacheTimeAsync() => _indexedDb.GetLastCacheTimeAsync();

    public async Task<DateTime?> GetServerUpdatedAsync(int ticketUid)
    {
        if (ticketUid <= 0) return null;
        var iso = await FetchServerUpdatedAsync(ticketUid);
        return TryParseUtc(iso ?? string.Empty, out var dt) ? dt : null;
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

    public Task EnqueueCreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId, DateTime? dueDate = null, IReadOnlyList<string>? checklist = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "CreateTicket",
            Content = subject,
            Subject = subject,
            Issue = issue,
            TypeId = typeId,
            PriorityId = priorityId,
            GroupId = groupId,
            TargetUserId = assigneeId,
            // Capture the author NOW so a drain under a different session doesn't
            // attribute the ticket to whoever happens to be logged in then (#280).
            OwnerId = _apiService.CurrentUserId,
            DueDate = dueDate?.ToString("O"),
            ChecklistTitles = checklist is { Count: > 0 } ? checklist.ToList() : null
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

    public Task EnqueueSetAdditionalAssigneesAsync(string ticketId, int ticketUid, IEnumerable<string> userIds, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "SetAdditionalAssignees",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TargetUserIds = userIds.ToList(),
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

    public Task EnqueueUpdateTicketFieldsAsync(string ticketId, int ticketUid, string? subject, string? issue, string? priorityId, string? typeId, string? groupId, DateTime? dueDate, DateTime? ticketUpdatedAt = null, bool dueDateCleared = false) =>
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
            DueDateCleared = dueDateCleared && dueDate == null,
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

    public Task EnqueueAddTagAsync(string ticketId, int ticketUid, string tagId, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "AddTag",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TagId = tagId,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    public Task EnqueueRemoveTagAsync(string ticketId, int ticketUid, string tagId, DateTime? ticketUpdatedAt = null) =>
        EnqueueAsync(new PendingActionDto
        {
            ActionType = "RemoveTag",
            TicketId = ticketId,
            TicketUid = ticketUid,
            TagId = tagId,
            TicketUpdatedAt = ticketUpdatedAt?.ToString("O")
        });

    private async Task EnqueueAsync(PendingActionDto action)
    {
        action.CreatedAt = DateTime.UtcNow.ToString("O");
        await _indexedDb.EnqueuePendingActionAsync(JsonSerializer.Serialize(action, JsonOptions));
        await UpdatePendingCount();
    }

    // Guards against overlapping drains (e.g. the startup pass and a
    // connectivity-restored pass firing at once). WASM is single-threaded, so
    // a plain flag is sufficient.
    private bool _syncing;

    public async Task<bool> SyncPendingActionsAsync()
    {
        if (_syncing) return false;
        _syncing = true;
        try
        {
            var json = await _indexedDb.GetPendingActionsAsync();
            var actions = JsonSerializer.Deserialize<PendingActionDto[]>(json, JsonOptions) ?? [];

            var allSuccess = true;
            var now = DateTime.UtcNow;

            // Tickets whose head-of-line action this drain has NOT applied (it is
            // conflicted, in backoff, or just failed). A later queued action for
            // the same ticket must not leapfrog it, otherwise the older mutation
            // applies last on a subsequent drain and overwrites the newer state
            // (#204). Queue order is insertion order, so blocking here preserves
            // per-ticket ordering across drains.
            var blockedTickets = new HashSet<string>();
            void Block(PendingActionDto a)
            {
                if (!string.IsNullOrEmpty(a.TicketId)) blockedTickets.Add(a.TicketId!);
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];

                // Skip already-conflicted actions (user must resolve them explicitly),
                // and block later same-ticket actions behind them.
                if (action.IsConflicted)
                {
                    Block(action);
                    continue;
                }

                // An earlier action for this ticket is still pending this drain —
                // defer this one too so ordering is preserved (#204).
                if (!string.IsNullOrEmpty(action.TicketId) && blockedTickets.Contains(action.TicketId!))
                {
                    allSuccess = false;
                    continue;
                }

                // Respect backoff schedule
                if (!string.IsNullOrEmpty(action.NextRetryAt)
                    && TryParseUtc(action.NextRetryAt, out var nextRetry)
                    && nextRetry > now)
                {
                    Block(action);
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
                            Block(action);
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
                        // Our own apply just bumped the server's 'updated' timestamp.
                        // Advance the captured baseline of later queued actions for the
                        // same ticket so their conflict check doesn't misread our own
                        // change as a concurrent edit by someone else (issue #197).
                        await RefreshSameTicketBaselinesAsync(action, actions, i + 1);
                    }
                    else
                    {
                        Block(action);
                        allSuccess = false;
                        await HandleFailureAsync(action, "API call returned failure", null);
                    }
                }
                catch (Exception ex)
                {
                    Block(action);
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
        finally
        {
            _syncing = false;
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

                // Advance the baseline of any remaining queued action for the same
                // ticket: our force-apply just bumped the server `updated`, so
                // without this the next drain re-flags those actions as a conflict
                // (drift now including our own change) and the user has to
                // force-apply each one separately (#285).
                var pendingJson = await _indexedDb.GetPendingActionsAsync();
                var pending = JsonSerializer.Deserialize<PendingActionDto[]>(pendingJson, JsonOptions) ?? [];
                await RefreshSameTicketBaselinesAsync(action, pending, 0);
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
        "AddComment" => await _apiService.AddCommentAsync(action.TicketUid.ToString(), action.OwnerId!, action.Content!),
        "AddNote" => await _apiService.AddNoteAsync(action.TicketUid.ToString(), action.OwnerId!, action.Content!),
        "AssignTicket" => await _apiService.AssignTicketAsync(action.TicketId!, action.TicketUid, action.TargetUserId!),
        "ClearAssignee" => await _apiService.ClearTicketAssigneeAsync(action.TicketId!, action.TicketUid),
        "SetAdditionalAssignees" => await _apiService.SetAdditionalAssigneesAsync(action.TicketId!, action.TargetUserIds ?? []),
        "UpdateStatus" => await _apiService.UpdateTicketStatusAsync(action.TicketId!, action.TicketUid, action.StatusId!),
        "CreateTicket" => await _apiService.CreateTicketAsync(
            action.Subject ?? action.Content ?? "", action.Issue, action.TypeId, action.PriorityId, action.GroupId, action.TargetUserId,
            ParseOptionalDate(action.DueDate), action.ChecklistTitles, action.OwnerId) != null,
        "UpdateTicketFields" => await ApplyUpdateTicketFieldsAsync(action),
        "DeleteTicket" => await _apiService.DeleteTicketAsync(action.TicketId!, action.TicketUid),
        "UploadAttachment" => await ApplyUploadAttachmentAsync(action),
        "AddTag" => await _apiService.AddTagToTicketAsync(action.TicketId!, action.TicketUid, action.TagId!),
        "RemoveTag" => await _apiService.RemoveTagFromTicketAsync(action.TicketId!, action.TicketUid, action.TagId!),
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
        // Tri-state due date: DueDate set = change it, DueDateCleared = send
        // an explicit null (ticket.DueDate stays MinValue, which
        // EditTicketAsync serializes as null), neither = omit the key so an
        // unrelated queued edit doesn't clobber the server-side due date.
        var hasDueDate = false;
        if (!string.IsNullOrEmpty(action.DueDate) && DateTime.TryParse(action.DueDate, out var dd))
        {
            ticket.DueDate = dd;
            hasDueDate = true;
        }

        return _apiService.EditTicketAsync(ticket, includeDueDate: hasDueDate || action.DueDateCleared);
    }

    // DTO dates are stored as round-trip ("O") strings; same lenient parse
    // as ApplyUpdateTicketFieldsAsync uses for its DueDate.
    private static DateTime? ParseOptionalDate(string? value) =>
        !string.IsNullOrEmpty(value) && DateTime.TryParse(value, out var dt) ? dt : null;

    private async Task<bool> ApplyUploadAttachmentAsync(PendingActionDto action)
    {
        if (string.IsNullOrEmpty(action.TicketId) || string.IsNullOrEmpty(action.FileContentBase64) || string.IsNullOrEmpty(action.FileName))
            return false;

        var bytes = Convert.FromBase64String(action.FileContentBase64);
        using var stream = new MemoryStream(bytes);
        // Pass the stored content type instead of re-deriving from the filename
        // (which would octet-stream webp/heic/… and get rejected) — #284.
        return await _apiService.UploadAttachmentAsync(action.TicketId, stream, action.FileName, action.FileContentType);
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

        // trudesk v1 answers a deleted/unknown ticket with HTTP 200 +
        // {success:false,error:'Invalid Ticket'} instead of a 404, so the status
        // check above never catches it and the action would silently retry until
        // it is dropped. Inspect the body: an explicit success:false — or a 2xx
        // payload carrying no ticket at all — means the ticket is gone. (v2 returns
        // the ticket on success and a real 404 otherwise, so this stays inert for v2.)
        if (IsTicketMissing(response.body))
        {
            if (action.ActionType == "DeleteTicket")
                return null; // someone else already deleted it — our goal is met
            return new ConflictResult(
                ConflictType.TicketDeleted,
                "Das Ticket wurde serverseitig gelöscht, seit die Aktion erstellt wurde.");
        }

        // Additive actions (comment/note) can only conflict with the ticket being
        // gone or access lost — both handled by the 404/401/403 checks above. They
        // can never conflict with a mere field edit, so skip the updated-timestamp
        // drift check that would otherwise strand them in the conflict queue (#214).
        if (action.ActionType is "AddComment" or "AddNote")
            return null;

        // Happy path: parse body and look for update/status drift.
        try
        {
            using var doc = JsonDocument.Parse(response.body);

            // Navigate to ticket data (handles both v1 and v2 response formats)
            var ticketEl = NavigateToTicketElement(doc.RootElement);

            // No expected baseline to compare against — skip.
            if (string.IsNullOrEmpty(action.TicketUpdatedAt))
                return null;

            if (!ticketEl.TryGetProperty("updated", out var updatedEl))
                return null;

            var serverUpdated = updatedEl.GetString();
            if (string.IsNullOrEmpty(serverUpdated))
                return null;

            // Compare as instants, not strings. The baseline is the client's
            // DateTime.ToString("O") (7 fractional digits) while the server
            // emits its own ISO format (often 3 digits), so a raw string
            // compare almost never matches and would flag EVERY queued action
            // as a conflict. A sub-second tolerance absorbs the format/rounding
            // drift; we fall back to the string compare only if either side is
            // unparseable.
            var sameInstant = TryParseUtc(serverUpdated, out var serverDt)
                              && TryParseUtc(action.TicketUpdatedAt, out var baselineDt)
                ? Math.Abs((serverDt - baselineDt).TotalSeconds) < 1
                : serverUpdated == action.TicketUpdatedAt;
            if (sameInstant)
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

    // After an action for a ticket applies, every later queued action for that
    // same ticket still holds the pre-drain baseline while the server has moved
    // on by exactly our own change. Re-fetch the ticket's current 'updated' and
    // write it as the new baseline for those successors — in memory (so this
    // drain sees it) and persisted (so a later drain does too). If another user
    // edited in the meantime, the successor's own check still sees a newer value
    // than this refreshed baseline and correctly reports a conflict.
    private async Task RefreshSameTicketBaselinesAsync(PendingActionDto applied, PendingActionDto[] actions, int fromIndex)
    {
        if (applied.TicketUid <= 0)
            return;

        // Only pay for the extra fetch when a later action actually targets the
        // same ticket and carries a baseline to refresh.
        var hasSuccessor = false;
        for (var j = fromIndex; j < actions.Length; j++)
        {
            var s = actions[j];
            if (!s.IsConflicted && s.TicketUid == applied.TicketUid && !string.IsNullOrEmpty(s.TicketUpdatedAt))
            {
                hasSuccessor = true;
                break;
            }
        }
        if (!hasSuccessor)
            return;

        var newBaseline = await FetchServerUpdatedAsync(applied.TicketUid);
        if (string.IsNullOrEmpty(newBaseline))
            return;

        for (var j = fromIndex; j < actions.Length; j++)
        {
            var succ = actions[j];
            if (succ.IsConflicted || succ.TicketUid != applied.TicketUid || string.IsNullOrEmpty(succ.TicketUpdatedAt))
                continue;
            succ.TicketUpdatedAt = newBaseline;
            if (succ.Id > 0)
                await _indexedDb.UpdateActionBaselineAsync(succ.Id, newBaseline);
        }
    }

    private async Task<string?> FetchServerUpdatedAsync(int ticketUid)
    {
        try
        {
            var (statusCode, body) = await _apiService.GetTicketRawAsync(ticketUid.ToString());
            if (statusCode is < 200 or >= 300)
                return null;
            using var doc = JsonDocument.Parse(body);
            var ticketEl = NavigateToTicketElement(doc.RootElement);
            if (ticketEl.TryGetProperty("updated", out var updatedEl))
                return updatedEl.GetString();
        }
        catch
        {
            // Best-effort: if we can't read the new baseline, leave successors as-is.
        }
        return null;
    }

    // A 2xx GET /tickets/:uid whose body doesn't actually contain a ticket.
    // trudesk v1 uses {success:false} for a deleted/unknown ticket (HTTP 200)
    // instead of a 404; a payload with no ticket identity (_id/uid) counts too.
    private static bool IsTicketMissing(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("success", out var successEl)
                && successEl.ValueKind == JsonValueKind.False)
                return true;

            var ticketEl = NavigateToTicketElement(root);
            if (ticketEl.ValueKind != JsonValueKind.Object)
                return true;
            return !ticketEl.TryGetProperty("_id", out _) && !ticketEl.TryGetProperty("uid", out _);
        }
        catch
        {
            // Unparseable — can't prove the ticket is gone; let the apply step try.
            return false;
        }
    }

    // trudesk wraps the ticket differently across endpoints/versions
    // ({ data: {...} } | { ticket: {...} } | the ticket object itself).
    private static JsonElement NavigateToTicketElement(JsonElement root)
    {
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
            return dataEl;
        if (root.TryGetProperty("ticket", out var tEl))
            return tEl;
        return root;
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
            // A dropped CreateTicket loses the user's authored content entirely
            // (the draft was removed at enqueue time and the ticket never got a
            // server id). Preserve the subject/issue in the error log so it stays
            // recoverable from the Settings sync-log diagnostics (#283).
            var detail = action.ActionType == "CreateTicket"
                ? $" [Betreff: \"{action.Subject ?? action.Content}\" | Beschreibung: {Truncate(action.Issue, 500)}]"
                : string.Empty;
            await AppendLogAsync("error", action, $"Gave up after {newRetryCount} failed attempts: {errorMessage}{detail}", exception);
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

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max] + "…";
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
