using System.Text.Json;
using NSubstitute;
using THWTicketApp.Shared.Data;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

public class SyncServiceTests
{
    private readonly IIndexedDbService _db = Substitute.For<IIndexedDbService>();
    private readonly ITrueDeskApiService _api = Substitute.For<ITrueDeskApiService>();
    private readonly AppStateService _state = new();
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _sut = new SyncService(_db, _api, _state);
        _db.GetPendingActionsAsync().Returns("[]");
        _db.GetPendingActionCountAsync().Returns(0);
    }

    private static PendingActionDto DeserializeEnqueued(string json) =>
        JsonSerializer.Deserialize<PendingActionDto>(json, SyncService.JsonOptions)!;

    private async Task<PendingActionDto> CaptureEnqueuedAsync(Func<Task> enqueue)
    {
        string? captured = null;
        await _db.EnqueuePendingActionAsync(Arg.Do<string>(s => captured = s));
        await enqueue();
        Assert.NotNull(captured);
        return DeserializeEnqueued(captured!);
    }

    // ---------------------------------------------------------------------
    // Enqueue tests — every new action type produces a correctly-shaped DTO
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EnqueueStatusAsync_producesUpdateStatusDto()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueStatusAsync("t1", 1001, "status-new", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("UpdateStatus", dto.ActionType);
        Assert.Equal("t1", dto.TicketId);
        Assert.Equal(1001, dto.TicketUid);
        Assert.Equal("status-new", dto.StatusId);
        Assert.NotNull(dto.TicketUpdatedAt);
        Assert.NotNull(dto.CreatedAt);
    }

    [Fact]
    public async Task EnqueueClearAssigneeAsync_producesClearAssigneeDto()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueClearAssigneeAsync("t1", 1001));

        Assert.Equal("ClearAssignee", dto.ActionType);
        Assert.Equal("t1", dto.TicketId);
        Assert.Null(dto.TargetUserId);
    }

    [Fact]
    public async Task EnqueueSetAdditionalAssigneesAsync_producesDtoWithUserIdList()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueSetAdditionalAssigneesAsync("t1", 1001, ["u1", "u2"], new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("SetAdditionalAssignees", dto.ActionType);
        Assert.Equal("t1", dto.TicketId);
        Assert.Equal(1001, dto.TicketUid);
        Assert.Equal(new List<string> { "u1", "u2" }, dto.TargetUserIds);
        Assert.NotNull(dto.TicketUpdatedAt);
        Assert.NotNull(dto.CreatedAt);
    }

    [Fact]
    public async Task EnqueueSetAdditionalAssigneesAsync_emptyListSurvivesRoundTrip()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueSetAdditionalAssigneesAsync("t1", 1001, []));

        Assert.Equal("SetAdditionalAssignees", dto.ActionType);
        Assert.NotNull(dto.TargetUserIds);
        Assert.Empty(dto.TargetUserIds!);
    }

    [Fact]
    public async Task EnqueueUpdateTicketFieldsAsync_producesDtoWithAllProvidedFields()
    {
        var due = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueUpdateTicketFieldsAsync("t1", 1001, "new subj", "new issue", "p1", "tt1", "g1", due));

        Assert.Equal("UpdateTicketFields", dto.ActionType);
        Assert.Equal("new subj", dto.Subject);
        Assert.Equal("new issue", dto.Issue);
        Assert.Equal("p1", dto.PriorityId);
        Assert.Equal("tt1", dto.TypeId);
        Assert.Equal("g1", dto.GroupId);
        Assert.Equal(due, DateTime.Parse(dto.DueDate!).ToUniversalTime());
        Assert.False(dto.DueDateCleared);
    }

    [Fact]
    public async Task EnqueueUpdateTicketFieldsAsync_dueDateClearedSetsFlag()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueUpdateTicketFieldsAsync("t1", 1001, null, null, null, null, null, null, null, dueDateCleared: true));

        Assert.Equal("UpdateTicketFields", dto.ActionType);
        Assert.Null(dto.DueDate);
        Assert.True(dto.DueDateCleared);
    }

    [Fact]
    public async Task EnqueueUpdateTicketFieldsAsync_nullDueDateWithoutClearFlag_meansUnchanged()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueUpdateTicketFieldsAsync("t1", 1001, "subj", null, null, null, null, null));

        Assert.Null(dto.DueDate);
        Assert.False(dto.DueDateCleared);
    }

    [Fact]
    public void PendingActionDto_withoutDueDateClearedProperty_deserializesAsFalse()
    {
        // Backwards compatibility: actions queued in IndexedDB before the
        // flag existed must keep behaving like "due date unchanged".
        var dto = DeserializeEnqueued(
            "{\"id\":1,\"actionType\":\"UpdateTicketFields\",\"ticketId\":\"t1\",\"ticketUid\":1001,\"subject\":\"s\"}");

        Assert.False(dto.DueDateCleared);
        Assert.Null(dto.DueDate);
    }

    [Fact]
    public async Task EnqueueDeleteTicketAsync_producesDeleteTicketDto()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueDeleteTicketAsync("t1", 1001));

        Assert.Equal("DeleteTicket", dto.ActionType);
        Assert.Equal("t1", dto.TicketId);
    }

    [Fact]
    public async Task EnqueueUploadAttachmentAsync_storesBase64EncodedContent()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueUploadAttachmentAsync("t1", 1001, "file.txt", bytes, "text/plain"));

        Assert.Equal("UploadAttachment", dto.ActionType);
        Assert.Equal("file.txt", dto.FileName);
        Assert.Equal("text/plain", dto.FileContentType);
        Assert.Equal(bytes, Convert.FromBase64String(dto.FileContentBase64!));
    }

    [Fact]
    public async Task EnqueueUploadAttachmentAsync_rejectsEmptyContent()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.EnqueueUploadAttachmentAsync("t1", 1001, "file.txt", [], "text/plain"));
    }

    [Fact]
    public async Task EnqueueUploadAttachmentAsync_rejectsOversizedContent()
    {
        var oversized = new byte[SyncService.MaxAttachmentBytes + 1];
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.EnqueueUploadAttachmentAsync("t1", 1001, "big.bin", oversized, "application/octet-stream"));
    }

    [Fact]
    public async Task EnqueueUploadAttachmentAsync_acceptsExactLimit()
    {
        var atLimit = new byte[SyncService.MaxAttachmentBytes];
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueUploadAttachmentAsync("t1", 1001, "big.bin", atLimit, "application/octet-stream"));
        Assert.Equal("UploadAttachment", dto.ActionType);
    }

    [Fact]
    public async Task EnqueueCreateTicketAsync_producesDtoWithDueDateAndChecklist()
    {
        var dueDate = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueCreateTicketAsync("Pumpe defekt", "Macht Geräusche", "type1", "prio1", "g1", "u1",
                dueDate, ["Schritt 1", "Schritt 2"]));

        Assert.Equal("CreateTicket", dto.ActionType);
        Assert.Equal("Pumpe defekt", dto.Subject);
        Assert.Equal("Macht Geräusche", dto.Issue);
        Assert.Equal("type1", dto.TypeId);
        Assert.Equal("prio1", dto.PriorityId);
        Assert.Equal("g1", dto.GroupId);
        Assert.Equal("u1", dto.TargetUserId);
        Assert.Equal(dueDate.ToString("O"), dto.DueDate);
        Assert.Equal(new List<string> { "Schritt 1", "Schritt 2" }, dto.ChecklistTitles);
        Assert.NotNull(dto.CreatedAt);
    }

    [Fact]
    public async Task EnqueueCreateTicketAsync_withoutOptionals_leavesDueDateAndChecklistNull()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueCreateTicketAsync("Subject", "Issue", null, null, "g1", null));

        Assert.Equal("CreateTicket", dto.ActionType);
        Assert.Null(dto.DueDate);
        Assert.Null(dto.ChecklistTitles);
    }

    [Fact]
    public async Task EnqueueCreateTicketAsync_emptyChecklist_storedAsNull()
    {
        var dto = await CaptureEnqueuedAsync(() =>
            _sut.EnqueueCreateTicketAsync("Subject", "Issue", null, null, "g1", null, null, []));

        Assert.Null(dto.ChecklistTitles);
    }

    // ---------------------------------------------------------------------
    // Sync apply — switch mapping for new action types
    // ---------------------------------------------------------------------

    private void SetupQueuedActions(params PendingActionDto[] actions)
    {
        var json = JsonSerializer.Serialize(actions, SyncService.JsonOptions);
        _db.GetPendingActionsAsync().Returns(json);
    }

    [Fact]
    public async Task Sync_dispatchesDeleteTicketToApi()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 1,
            ActionType = "DeleteTicket",
            TicketId = "t1",
            TicketUid = 1001
        });
        _api.DeleteTicketAsync("t1", 1001).Returns(true);

        var result = await _sut.SyncPendingActionsAsync();

        Assert.True(result);
        await _api.Received(1).DeleteTicketAsync("t1", 1001);
        await _db.Received(1).RemovePendingActionAsync(1);
    }

    [Fact]
    public async Task Sync_dispatchesUpdateTicketFieldsToEditTicketWithMergedTicket()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 2,
            ActionType = "UpdateTicketFields",
            TicketId = "t1",
            TicketUid = 1001,
            Subject = "updated",
            Issue = "body",
            PriorityId = "p1",
            TypeId = "tt1",
            GroupId = "g1"
        });
        _api.EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()).Returns(true);

        await _sut.SyncPendingActionsAsync();

        // No DueDate and no clear flag — the apply must NOT touch the
        // server-side due date (includeDueDate: false), otherwise an
        // unrelated queued edit clobbers it to null.
        await _api.Received(1).EditTicketAsync(Arg.Is<Ticket>(t =>
            t.Id == "t1" &&
            t.Uid == 1001 &&
            t.Subject == "updated" &&
            t.Issue == "body" &&
            t.Priority!.Id == "p1" &&
            t.Type!.Id == "tt1" &&
            t.Group!.Id == "g1"), false);
    }

    [Fact]
    public async Task Sync_updateTicketFieldsWithDueDate_sendsDueDate()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 7,
            ActionType = "UpdateTicketFields",
            TicketId = "t1",
            TicketUid = 1001,
            DueDate = "2030-01-15T00:00:00.0000000Z"
        });
        _api.EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()).Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).EditTicketAsync(Arg.Is<Ticket>(t =>
            t.Id == "t1" &&
            t.DueDate.ToUniversalTime() == new DateTime(2030, 1, 15, 0, 0, 0, DateTimeKind.Utc)), true);
    }

    [Fact]
    public async Task Sync_updateTicketFieldsWithDueDateCleared_sendsExplicitClear()
    {
        // An offline "remove due date" must arrive at the API exactly like
        // the online path: EditTicketAsync with DueDate == MinValue and
        // includeDueDate true, which serializes dueDate as explicit null.
        SetupQueuedActions(new PendingActionDto
        {
            Id = 8,
            ActionType = "UpdateTicketFields",
            TicketId = "t1",
            TicketUid = 1001,
            DueDateCleared = true
        });
        _api.EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()).Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).EditTicketAsync(Arg.Is<Ticket>(t =>
            t.Id == "t1" && t.DueDate == DateTime.MinValue), true);
        await _db.Received(1).RemovePendingActionAsync(8);
    }

    [Fact]
    public async Task Sync_dispatchesUploadAttachmentWithDecodedStream()
    {
        var payload = new byte[] { 10, 20, 30 };
        SetupQueuedActions(new PendingActionDto
        {
            Id = 3,
            ActionType = "UploadAttachment",
            TicketId = "t1",
            TicketUid = 1001,
            FileName = "a.bin",
            FileContentType = "application/octet-stream",
            FileContentBase64 = Convert.ToBase64String(payload)
        });

        // Capture stream bytes during the call — SyncService disposes the stream afterwards.
        byte[]? capturedBytes = null;
        _api.UploadAttachmentAsync(
            Arg.Any<string>(),
            Arg.Do<Stream>(s =>
            {
                using var copy = new MemoryStream();
                s.Position = 0;
                s.CopyTo(copy);
                capturedBytes = copy.ToArray();
            }),
            Arg.Any<string>()).Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).UploadAttachmentAsync("t1", Arg.Any<Stream>(), "a.bin");
        Assert.NotNull(capturedBytes);
        Assert.Equal(payload, capturedBytes);
    }

    [Fact]
    public async Task Sync_dispatchesSetAdditionalAssigneesToApi()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 4,
            ActionType = "SetAdditionalAssignees",
            TicketId = "t1",
            TicketUid = 1001,
            TargetUserIds = ["u1", "u2"]
        });
        _api.SetAdditionalAssigneesAsync("t1", Arg.Any<IEnumerable<string>>()).Returns(true);

        var result = await _sut.SyncPendingActionsAsync();

        Assert.True(result);
        await _api.Received(1).SetAdditionalAssigneesAsync("t1",
            Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u1", "u2" })));
        await _db.Received(1).RemovePendingActionAsync(4);
    }

    [Fact]
    public async Task Sync_dispatchesCreateTicketWithDueDateAndChecklist()
    {
        var dueDate = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        SetupQueuedActions(new PendingActionDto
        {
            Id = 5,
            ActionType = "CreateTicket",
            Subject = "Pumpe defekt",
            Issue = "Macht Geräusche",
            TypeId = "type1",
            PriorityId = "prio1",
            GroupId = "g1",
            TargetUserId = "u1",
            DueDate = dueDate.ToString("O"),
            ChecklistTitles = ["Schritt 1", "Schritt 2"]
        });
        _api.CreateTicketAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(), Arg.Any<IReadOnlyList<string>?>())
            .Returns(new TicketCreateResult("new-id", 4711));

        var result = await _sut.SyncPendingActionsAsync();

        Assert.True(result);
        await _api.Received(1).CreateTicketAsync(
            "Pumpe defekt", "Macht Geräusche", "type1", "prio1", "g1", "u1",
            Arg.Is<DateTime?>(d => d.HasValue),
            Arg.Is<IReadOnlyList<string>?>(c => c != null && c.SequenceEqual(new[] { "Schritt 1", "Schritt 2" })));
        await _db.Received(1).RemovePendingActionAsync(5);
    }

    [Fact]
    public async Task Sync_createTicketWithoutOptionals_passesNulls()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 6,
            ActionType = "CreateTicket",
            Subject = "Subject",
            Issue = "Issue",
            GroupId = "g1"
        });
        _api.CreateTicketAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(), Arg.Any<IReadOnlyList<string>?>())
            .Returns(new TicketCreateResult("new-id", 4712));

        var result = await _sut.SyncPendingActionsAsync();

        Assert.True(result);
        await _api.Received(1).CreateTicketAsync(
            "Subject", "Issue", null, null, "g1", null, null, null);
        await _db.Received(1).RemovePendingActionAsync(6);
    }

    [Fact]
    public async Task Sync_createTicketNullResult_isFailureAndSchedulesRetry()
    {
        // CreateTicketAsync returns null when the server rejects the create —
        // the action must stay queued with retry state, not be removed.
        SetupQueuedActions(new PendingActionDto
        {
            Id = 7,
            ActionType = "CreateTicket",
            Subject = "Subject",
            GroupId = "g1"
        });
        _api.CreateTicketAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(), Arg.Any<IReadOnlyList<string>?>())
            .Returns((TicketCreateResult?)null);

        var result = await _sut.SyncPendingActionsAsync();

        Assert.False(result);
        await _db.DidNotReceive().RemovePendingActionAsync(7);
        await _db.Received(1).UpdateRetryStateAsync(7, Arg.Any<string>(), 1, Arg.Any<string>());
    }

    // ---------------------------------------------------------------------
    // Retry / backoff / give-up
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Sync_onFailure_schedulesNextRetryUsingBackoff()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 10,
            ActionType = "AddComment",
            TicketId = "t1",
            TicketUid = 42,
            OwnerId = "u1",
            Content = "hi",
            RetryCount = 0
        });
        _api.AddCommentAsync("42", "u1", "hi").Returns(false);

        var before = DateTime.UtcNow;
        await _sut.SyncPendingActionsAsync();
        var after = DateTime.UtcNow;

        var expectedMin = before + SyncService.RetryDelays[0] - TimeSpan.FromSeconds(1);
        var expectedMax = after + SyncService.RetryDelays[0] + TimeSpan.FromSeconds(1);
        await _db.Received(1).UpdateRetryStateAsync(10,
            Arg.Is<string>(iso => IsIsoWithinRange(iso, expectedMin, expectedMax)),
            1,
            Arg.Any<string>());
    }

    private static bool IsIsoWithinRange(string iso, DateTime min, DateTime max)
    {
        if (!DateTime.TryParse(iso, out var dt)) return false;
        var utc = dt.ToUniversalTime();
        return utc >= min && utc <= max;
    }

    [Fact]
    public async Task Sync_skipsActionWithFutureNextRetryAt()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 11,
            ActionType = "AddComment",
            TicketId = "t1",
            OwnerId = "u1",
            Content = "hi",
            RetryCount = 1,
            NextRetryAt = DateTime.UtcNow.AddMinutes(10).ToString("O")
        });

        await _sut.SyncPendingActionsAsync();

        await _api.DidNotReceive().AddCommentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _db.DidNotReceive().UpdateRetryStateAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>());
        await _db.DidNotReceive().RemovePendingActionAsync(11);
    }

    [Fact]
    public async Task Sync_retriesActionWhenNextRetryAtIsPast()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 12,
            ActionType = "AddComment",
            TicketId = "t1",
            TicketUid = 42,
            OwnerId = "u1",
            Content = "hi",
            RetryCount = 1,
            NextRetryAt = DateTime.UtcNow.AddSeconds(-5).ToString("O")
        });
        _api.AddCommentAsync("42", "u1", "hi").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).AddCommentAsync("42", "u1", "hi");
        await _db.Received(1).RemovePendingActionAsync(12);
    }

    [Fact]
    public async Task Sync_dropsActionAfterExhaustingAllRetries()
    {
        var lastAttempt = SyncService.RetryDelays.Length; // failing for the (N+1)th time
        SetupQueuedActions(new PendingActionDto
        {
            Id = 13,
            ActionType = "AddComment",
            TicketId = "t1",
            TicketUid = 42,
            OwnerId = "u1",
            Content = "hi",
            RetryCount = lastAttempt
        });
        _api.AddCommentAsync("42", "u1", "hi").Returns(false);

        await _sut.SyncPendingActionsAsync();

        await _db.Received(1).RemovePendingActionAsync(13);
        await _db.DidNotReceive().UpdateRetryStateAsync(13, Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>());
        await _db.Received().AppendSyncLogAsync(Arg.Is<string>(s => s.Contains("Gave up")));
    }

    [Fact]
    public async Task Sync_skipsActionsAlreadyMarkedConflicted()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 14,
            ActionType = "AddComment",
            TicketId = "t1",
            OwnerId = "u1",
            Content = "hi",
            IsConflicted = true,
            ConflictReason = "prior conflict"
        });

        await _sut.SyncPendingActionsAsync();

        await _api.DidNotReceive().AddCommentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _db.DidNotReceive().RemovePendingActionAsync(14);
    }

    [Fact]
    public async Task Sync_successWritesInfoLogEntry()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 15,
            ActionType = "DeleteTicket",
            TicketId = "t1",
            TicketUid = 1001
        });
        _api.DeleteTicketAsync("t1", 1001).Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _db.Received().AppendSyncLogAsync(Arg.Is<string>(s => s.Contains("\"level\":\"info\"") && s.Contains("Synced successfully")));
    }

    // ---------------------------------------------------------------------
    // R2.2 — Conflict detection with typed results
    // ---------------------------------------------------------------------

    // #214: additive comment/note actions must never be conflict-checked, so an
    // unrelated field edit on the ticket can't strand them in the conflict queue.
    [Fact]
    public async Task Sync_addComment_isNotConflictCheckedOrBlockedByTicketDrift()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 1,
            ActionType = "AddComment",
            TicketId = "t1",
            TicketUid = 1001,
            OwnerId = "u1",
            Content = "hi",
            TicketUpdatedAt = "2026-04-01T10:00:00.0000000Z"
        });
        // Ticket has drifted (an unrelated field was edited server-side).
        _api.GetTicketRawAsync("1001").Returns((200, "{\"_id\":\"t1\",\"updated\":\"2026-04-05T12:00:00.0000000Z\"}"));
        _api.AddCommentAsync("1001", "u1", "hi").Returns(true);
        PendingAction? conflict = null;
        _sut.ConflictDetected += pa => conflict = pa;

        await _sut.SyncPendingActionsAsync();

        // Despite the drift, the comment applies and is not flagged as a conflict.
        await _api.Received(1).AddCommentAsync("1001", "u1", "hi");
        await _db.Received(1).RemovePendingActionAsync(1);
        Assert.Null(conflict);
        await _db.DidNotReceive().MarkActionConflictedAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // #204: when an earlier action for a ticket fails, a later queued action for
    // the SAME ticket must be deferred, not applied out of order.
    [Fact]
    public async Task Sync_laterSameTicketAction_deferredWhenEarlierFails()
    {
        SetupQueuedActions(
            new PendingActionDto { Id = 1, ActionType = "UpdateStatus", TicketId = "t1", TicketUid = 1001, StatusId = "s1" },
            new PendingActionDto { Id = 2, ActionType = "UpdateStatus", TicketId = "t1", TicketUid = 1001, StatusId = "s2" });
        _api.UpdateTicketStatusAsync("t1", 1001, "s1").Returns(false); // earlier fails
        _api.UpdateTicketStatusAsync("t1", 1001, "s2").Returns(true);  // later would succeed

        await _sut.SyncPendingActionsAsync();

        // The later action must NOT leapfrog the still-pending earlier one.
        await _api.DidNotReceive().UpdateTicketStatusAsync("t1", 1001, "s2");
        await _db.DidNotReceive().RemovePendingActionAsync(2);
    }

    // A later action for a DIFFERENT ticket is unaffected by an earlier failure.
    [Fact]
    public async Task Sync_laterDifferentTicketAction_stillAppliedWhenEarlierFails()
    {
        SetupQueuedActions(
            new PendingActionDto { Id = 1, ActionType = "UpdateStatus", TicketId = "t1", TicketUid = 1001, StatusId = "s1" },
            new PendingActionDto { Id = 2, ActionType = "UpdateStatus", TicketId = "t2", TicketUid = 1002, StatusId = "s2" });
        _api.UpdateTicketStatusAsync("t1", 1001, "s1").Returns(false);
        _api.UpdateTicketStatusAsync("t2", 1002, "s2").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).UpdateTicketStatusAsync("t2", 1002, "s2");
        await _db.Received(1).RemovePendingActionAsync(2);
    }

    // Default is a drift-eligible action (UpdateStatus). Comment/note actions are
    // intentionally exempt from the updated-timestamp drift check (#214), so they
    // are not a suitable default for the generic drift-detection tests.
    private PendingActionDto ActionTargetingTicket(string actionType = "UpdateStatus", string? statusId = null, string? ticketUpdatedAt = null)
        => new()
        {
            Id = 100,
            ActionType = actionType,
            TicketId = "t1",
            TicketUid = 1001,
            OwnerId = "u1",
            Content = "hi",
            StatusId = statusId,
            TicketUpdatedAt = ticketUpdatedAt ?? "2026-04-01T10:00:00.0000000Z"
        };

    [Fact]
    public async Task CheckConflict_ticketDeleted_returnsTicketDeletedType()
    {
        _api.GetTicketRawAsync("1001").Returns((404, "{\"success\":false,\"error\":\"not found\"}"));
        var result = await _sut.CheckConflictAsync(ActionTargetingTicket());

        Assert.NotNull(result);
        Assert.Equal(ConflictType.TicketDeleted, result!.Type);
    }

    [Fact]
    public async Task CheckConflict_ticketDeleted_isNotAConflictForDeleteTicketAction()
    {
        _api.GetTicketRawAsync("1001").Returns((404, ""));
        var result = await _sut.CheckConflictAsync(ActionTargetingTicket("DeleteTicket"));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task CheckConflict_authFailure_returnsPermissionRevoked(int statusCode)
    {
        _api.GetTicketRawAsync("1001").Returns((statusCode, "{\"error\":\"forbidden\"}"));
        var result = await _sut.CheckConflictAsync(ActionTargetingTicket());

        Assert.NotNull(result);
        Assert.Equal(ConflictType.PermissionRevoked, result!.Type);
    }

    [Fact]
    public async Task CheckConflict_transientError_returnsNullSoRetryLoopTakesOver()
    {
        _api.GetTicketRawAsync("1001").Returns((500, "boom"));
        var result = await _sut.CheckConflictAsync(ActionTargetingTicket());

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckConflict_sameUpdatedTimestamp_returnsNull()
    {
        var baseline = "2026-04-01T10:00:00.0000000Z";
        _api.GetTicketRawAsync("1001").Returns((200,
            $"{{\"_id\":\"t1\",\"updated\":\"{baseline}\",\"status\":{{\"_id\":\"s1\"}}}}"));

        var result = await _sut.CheckConflictAsync(ActionTargetingTicket(ticketUpdatedAt: baseline));

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckConflict_sameInstantDifferentPrecision_returnsNull()
    {
        // Server emits 3 fractional digits; the client baseline is
        // DateTime.ToString("O") with 7. Same instant — must NOT be a conflict
        // (a raw string compare here would wrongly flag every queued action).
        _api.GetTicketRawAsync("1001").Returns((200,
            "{\"_id\":\"t1\",\"updated\":\"2026-04-01T10:00:00.000Z\",\"status\":{\"_id\":\"s1\"}}"));

        var result = await _sut.CheckConflictAsync(
            ActionTargetingTicket(ticketUpdatedAt: "2026-04-01T10:00:00.0000000Z"));

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckConflict_driftedUpdatedTimestamp_returnsTicketUpdated()
    {
        _api.GetTicketRawAsync("1001").Returns((200,
            "{\"_id\":\"t1\",\"updated\":\"2026-04-05T12:00:00.0000000Z\",\"status\":{\"_id\":\"s1\"}}"));

        var result = await _sut.CheckConflictAsync(ActionTargetingTicket(ticketUpdatedAt: "2026-04-01T10:00:00.0000000Z"));

        Assert.NotNull(result);
        Assert.Equal(ConflictType.TicketUpdated, result!.Type);
    }

    // Regression for #197: two queued edits to the same ticket share one
    // pre-drain baseline. After the first applies, the server bumps 'updated';
    // the second must NOT be flagged as a conflict just because of our own change.
    [Fact]
    public async Task Sync_secondSameTicketAction_notFlaggedAsSelfConflict()
    {
        const string t0 = "2026-04-01T10:00:00.0000000Z";
        const string t1 = "2026-04-01T10:00:05.0000000Z"; // server bump after our 1st apply
        SetupQueuedActions(
            new PendingActionDto { Id = 1, ActionType = "UpdateTicketFields", TicketId = "t1", TicketUid = 1001, Subject = "title", TicketUpdatedAt = t0 },
            new PendingActionDto { Id = 2, ActionType = "UpdateTicketFields", TicketId = "t1", TicketUid = 1001, Issue = "desc", TicketUpdatedAt = t0 });

        // Call 1 = action #1's conflict check (sees T0 -> no conflict).
        // Call 2 = post-apply baseline refresh (sees T1, our own change).
        // Call 3 = action #2's conflict check (sees T1 vs refreshed baseline T1).
        _api.GetTicketRawAsync("1001").Returns(
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t0}\",\"status\":{{\"_id\":\"s1\"}}}}"),
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t1}\",\"status\":{{\"_id\":\"s1\"}}}}"),
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t1}\",\"status\":{{\"_id\":\"s1\"}}}}"));
        _api.EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()).Returns(true);

        PendingAction? conflict = null;
        _sut.ConflictDetected += pa => conflict = pa;

        var result = await _sut.SyncPendingActionsAsync();

        Assert.Null(conflict);                                          // no false self-conflict
        await _api.Received(2).EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>());
        await _db.Received(1).RemovePendingActionAsync(1);
        await _db.Received(1).RemovePendingActionAsync(2);
        await _db.Received(1).UpdateActionBaselineAsync(2, t1);         // successor baseline advanced + persisted
        await _db.DidNotReceive().MarkActionConflictedAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.True(result);
    }

    // The refresh must NOT hide a genuine concurrent edit: if another user
    // changes the ticket between our two applies, the second still conflicts.
    [Fact]
    public async Task Sync_secondSameTicketAction_stillConflictsOnForeignEdit()
    {
        const string t0 = "2026-04-01T10:00:00.0000000Z";
        const string t1 = "2026-04-01T10:00:05.0000000Z"; // our own apply
        const string t2 = "2026-04-01T10:00:30.0000000Z"; // someone else edits after
        SetupQueuedActions(
            new PendingActionDto { Id = 1, ActionType = "UpdateTicketFields", TicketId = "t1", TicketUid = 1001, Subject = "title", TicketUpdatedAt = t0 },
            new PendingActionDto { Id = 2, ActionType = "UpdateTicketFields", TicketId = "t1", TicketUid = 1001, Issue = "desc", TicketUpdatedAt = t0 });

        _api.GetTicketRawAsync("1001").Returns(
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t0}\",\"status\":{{\"_id\":\"s1\"}}}}"), // #1 check
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t1}\",\"status\":{{\"_id\":\"s1\"}}}}"), // refresh -> baseline t1
            (200, $"{{\"_id\":\"t1\",\"updated\":\"{t2}\",\"status\":{{\"_id\":\"s1\"}}}}")); // #2 check sees a newer value
        _api.EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()).Returns(true);

        PendingAction? conflict = null;
        _sut.ConflictDetected += pa => conflict = pa;

        await _sut.SyncPendingActionsAsync();

        Assert.NotNull(conflict);
        Assert.Equal(ConflictType.TicketUpdated, conflict!.ConflictType);
        await _api.Received(1).EditTicketAsync(Arg.Any<Ticket>(), Arg.Any<bool>()); // only #1 applied
        await _db.Received(1).MarkActionConflictedAsync(2, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CheckConflict_updateStatusAgainstDifferentServerStatus_returnsStatusChanged()
    {
        _api.GetTicketRawAsync("1001").Returns((200,
            "{\"_id\":\"t1\",\"updated\":\"2026-04-05T12:00:00.0000000Z\",\"status\":{\"_id\":\"s-other\"}}"));

        var action = ActionTargetingTicket("UpdateStatus", statusId: "s-wanted", ticketUpdatedAt: "2026-04-01T10:00:00.0000000Z");
        var result = await _sut.CheckConflictAsync(action);

        Assert.NotNull(result);
        Assert.Equal(ConflictType.StatusChanged, result!.Type);
    }

    [Fact]
    public async Task CheckConflict_updateStatusAgainstSameServerStatus_fallsBackToTicketUpdated()
    {
        // Someone else touched a different field but kept the same status.
        // Our UpdateStatus action could still be considered "done" by the server,
        // so it's classified as a generic TicketUpdated (not StatusChanged).
        _api.GetTicketRawAsync("1001").Returns((200,
            "{\"_id\":\"t1\",\"updated\":\"2026-04-05T12:00:00.0000000Z\",\"status\":{\"_id\":\"s-wanted\"}}"));

        var action = ActionTargetingTicket("UpdateStatus", statusId: "s-wanted", ticketUpdatedAt: "2026-04-01T10:00:00.0000000Z");
        var result = await _sut.CheckConflictAsync(action);

        Assert.NotNull(result);
        Assert.Equal(ConflictType.TicketUpdated, result!.Type);
    }

    [Fact]
    public async Task Sync_withTicketDeletedConflict_marksActionWithTypedReason()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 200,
            ActionType = "AddComment",
            TicketId = "t1",
            TicketUid = 1001,
            OwnerId = "u1",
            Content = "hi",
            TicketUpdatedAt = "2026-04-01T10:00:00.0000000Z"
        });
        _api.GetTicketRawAsync("1001").Returns((404, ""));

        await _sut.SyncPendingActionsAsync();

        await _db.Received(1).MarkActionConflictedAsync(200, Arg.Any<string>(), "TicketDeleted");
        await _api.DidNotReceive().AddCommentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task JsonOptions_omitsZeroIdOnSerialize()
    {
        var dto = new PendingActionDto { Id = 0, ActionType = "Test" };
        var json = JsonSerializer.Serialize(dto, SyncService.JsonOptions);
        Assert.DoesNotContain("\"id\"", json);
    }

    [Fact]
    public async Task JsonOptions_includesPositiveIdOnSerialize()
    {
        var dto = new PendingActionDto { Id = 42, ActionType = "Test" };
        var json = JsonSerializer.Serialize(dto, SyncService.JsonOptions);
        Assert.Contains("\"id\":42", json);
    }

    // ---------------------------------------------------------------------
    // R5.2 — Tag sync actions
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Sync_addTag_callsAddTagToTicketAsync()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 300,
            ActionType = "AddTag",
            TicketId = "t1",
            TicketUid = 42,
            TagId = "tag-abc"
        });
        _api.AddTagToTicketAsync("t1", 42, "tag-abc").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).AddTagToTicketAsync("t1", 42, "tag-abc");
        await _db.Received(1).RemovePendingActionAsync(300);
    }

    [Fact]
    public async Task Sync_removeTag_callsRemoveTagFromTicketAsync()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 301,
            ActionType = "RemoveTag",
            TicketId = "t1",
            TicketUid = 42,
            TagId = "tag-xyz"
        });
        _api.RemoveTagFromTicketAsync("t1", 42, "tag-xyz").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).RemoveTagFromTicketAsync("t1", 42, "tag-xyz");
        await _db.Received(1).RemovePendingActionAsync(301);
    }

    [Fact]
    public async Task Sync_addTag_failure_schedulesRetry()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 302,
            ActionType = "AddTag",
            TicketId = "t1",
            TicketUid = 42,
            TagId = "tag-abc",
            RetryCount = 0
        });
        _api.AddTagToTicketAsync("t1", 42, "tag-abc").Returns(false);

        await _sut.SyncPendingActionsAsync();

        await _db.Received(1).UpdateRetryStateAsync(302, Arg.Any<string>(), 1, Arg.Any<string?>());
        await _db.DidNotReceive().RemovePendingActionAsync(302);
    }
}
