using System.Text.Json;
using NSubstitute;
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
            Id = 1, ActionType = "DeleteTicket", TicketId = "t1", TicketUid = 1001
        });
        _api.DeleteTicketAsync("t1").Returns(true);

        var result = await _sut.SyncPendingActionsAsync();

        Assert.True(result);
        await _api.Received(1).DeleteTicketAsync("t1");
        await _db.Received(1).RemovePendingActionAsync(1);
    }

    [Fact]
    public async Task Sync_dispatchesUpdateTicketFieldsToEditTicketWithMergedTicket()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 2, ActionType = "UpdateTicketFields",
            TicketId = "t1", TicketUid = 1001,
            Subject = "updated", Issue = "body", PriorityId = "p1", TypeId = "tt1", GroupId = "g1"
        });
        _api.EditTicketAsync(Arg.Any<Ticket>()).Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).EditTicketAsync(Arg.Is<Ticket>(t =>
            t.Id == "t1" &&
            t.Uid == 1001 &&
            t.Subject == "updated" &&
            t.Issue == "body" &&
            t.Priority!.Id == "p1" &&
            t.Type!.Id == "tt1" &&
            t.Group!.Id == "g1"));
    }

    [Fact]
    public async Task Sync_dispatchesUploadAttachmentWithDecodedStream()
    {
        var payload = new byte[] { 10, 20, 30 };
        SetupQueuedActions(new PendingActionDto
        {
            Id = 3, ActionType = "UploadAttachment",
            TicketId = "t1", TicketUid = 1001,
            FileName = "a.bin", FileContentType = "application/octet-stream",
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

    // ---------------------------------------------------------------------
    // Retry / backoff / give-up
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Sync_onFailure_schedulesNextRetryUsingBackoff()
    {
        SetupQueuedActions(new PendingActionDto
        {
            Id = 10, ActionType = "AddComment",
            TicketId = "t1", OwnerId = "u1", Content = "hi",
            RetryCount = 0
        });
        _api.AddCommentAsync("t1", "u1", "hi").Returns(false);

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
            Id = 11, ActionType = "AddComment",
            TicketId = "t1", OwnerId = "u1", Content = "hi",
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
            Id = 12, ActionType = "AddComment",
            TicketId = "t1", OwnerId = "u1", Content = "hi",
            RetryCount = 1,
            NextRetryAt = DateTime.UtcNow.AddSeconds(-5).ToString("O")
        });
        _api.AddCommentAsync("t1", "u1", "hi").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _api.Received(1).AddCommentAsync("t1", "u1", "hi");
        await _db.Received(1).RemovePendingActionAsync(12);
    }

    [Fact]
    public async Task Sync_dropsActionAfterExhaustingAllRetries()
    {
        var lastAttempt = SyncService.RetryDelays.Length; // failing for the (N+1)th time
        SetupQueuedActions(new PendingActionDto
        {
            Id = 13, ActionType = "AddComment",
            TicketId = "t1", OwnerId = "u1", Content = "hi",
            RetryCount = lastAttempt
        });
        _api.AddCommentAsync("t1", "u1", "hi").Returns(false);

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
            Id = 14, ActionType = "AddComment",
            TicketId = "t1", OwnerId = "u1", Content = "hi",
            IsConflicted = true, ConflictReason = "prior conflict"
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
            Id = 15, ActionType = "DeleteTicket", TicketId = "t1", TicketUid = 1001
        });
        _api.DeleteTicketAsync("t1").Returns(true);

        await _sut.SyncPendingActionsAsync();

        await _db.Received().AppendSyncLogAsync(Arg.Is<string>(s => s.Contains("\"level\":\"info\"") && s.Contains("Synced successfully")));
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
}
