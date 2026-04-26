using System.Net;
using System.Text.Json;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

public class TrueDeskApiServiceTests
{
    private readonly CapturingHttpMessageHandler _handler = new();
    private readonly TrueDeskApiService _sut;

    public TrueDeskApiServiceTests()
    {
        var httpClient = new HttpClient(_handler);
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        var jsRuntime = Substitute.For<IJSRuntime>();
        var localStorage = new LocalStorageService(jsRuntime);
        _sut = new TrueDeskApiService(httpClient, settings, localStorage);
    }

    private HttpRequestMessage LastRequest => _handler.Requests[^1];
    private string LastBody => _handler.RequestBodies[^1];

    // -----------------------------------------------------------------
    // Teams
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetTeamsAsync_callsV2TeamsEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK, "[{\"_id\":\"team1\"}]");
        var body = await _sut.GetTeamsAsync();

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/teams", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("team1", body);
    }

    [Fact]
    public async Task GetTeamAsync_usesTeamIdInPath()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{}");
        await _sut.GetTeamAsync("abc123");

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/teams/abc123", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateTeamAsync_postsJsonPayload()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{}");
        var ok = await _sut.CreateTeamAsync(new() { ["name"] = "Alpha", ["members"] = new[] { "u1", "u2" } });

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/teams", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"name\":\"Alpha\"", LastBody);
        Assert.Contains("\"members\"", LastBody);
    }

    [Fact]
    public async Task UpdateTeamAsync_putsToTeamId()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.UpdateTeamAsync("abc123", new() { ["name"] = "Renamed" });

        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/teams/abc123", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("Renamed", LastBody);
    }

    [Fact]
    public async Task DeleteTeamAsync_deletesTeamId()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.DeleteTeamAsync("abc123");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Delete, LastRequest.Method);
        Assert.Equal("/api/v2/teams/abc123", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteTeamAsync_returnsFalseOnError()
    {
        _handler.SetDefault(HttpStatusCode.InternalServerError);
        var ok = await _sut.DeleteTeamAsync("abc123");
        Assert.False(ok);
    }

    // -----------------------------------------------------------------
    // Departments
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetDepartmentsAsync_callsV2DepartmentsEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK, "[]");
        await _sut.GetDepartmentsAsync();
        Assert.Equal("/api/v2/departments", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateDepartmentAsync_putsToDepartmentId()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.UpdateDepartmentAsync("d1", new() { ["name"] = "New" });
        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/departments/d1", LastRequest.RequestUri!.AbsolutePath);
    }

    // -----------------------------------------------------------------
    // Ticket Templates
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetTicketTemplatesAsync_callsV2TicketTemplatesEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK, "[]");
        await _sut.GetTicketTemplatesAsync();
        Assert.Equal("/api/v2/ticket-templates", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateTicketTemplateAsync_postsJsonPayload()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.CreateTicketTemplateAsync(new()
        {
            ["name"] = "Einsatz-Nachbereitung",
            ["subject"] = "Einsatz vom {date}"
        });
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/ticket-templates", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("Einsatz-Nachbereitung", LastBody);
    }

    [Fact]
    public async Task DeleteTicketTemplateAsync_callsV2Delete()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.DeleteTicketTemplateAsync("tpl1");
        Assert.Equal(HttpMethod.Delete, LastRequest.Method);
        Assert.Equal("/api/v2/ticket-templates/tpl1", LastRequest.RequestUri!.AbsolutePath);
    }

    // -----------------------------------------------------------------
    // Calendar
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetCalendarEventsAsync_forwardsStartAndEndAsQueryParams()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"events\":[]}");
        var start = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        await _sut.GetCalendarEventsAsync(start, end);

        Assert.Equal("/api/v2/calendar/events", LastRequest.RequestUri!.AbsolutePath);
        var query = LastRequest.RequestUri!.Query;
        // Backend reads req.query.start and req.query.end — exact names matter.
        Assert.Contains("start=", query);
        Assert.Contains("end=", query);
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.Contains("2026-05-01", query);
        Assert.Contains("2026-05-31", query);
    }

    // -----------------------------------------------------------------
    // Ticket tags (read-modify-write)
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateTicketTagsAsync_putsTagsArrayToTicket()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.UpdateTicketTagsAsync("t1", new[] { "tag-a", "tag-b" });

        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v1/tickets/t1", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("tag-a", LastBody);
        Assert.Contains("tag-b", LastBody);
    }

    [Fact]
    public async Task AddTagToTicketAsync_appendsToExistingTags()
    {
        _handler.RespondTo(HttpMethod.Get, "/api/v1/tickets/t1", HttpStatusCode.OK,
            "{\"_id\":\"t1\",\"tags\":[{\"_id\":\"tag-a\"}]}");
        _handler.RespondTo(HttpMethod.Put, "/api/v1/tickets/t1", HttpStatusCode.OK);

        var ok = await _sut.AddTagToTicketAsync("t1", "tag-b");

        Assert.True(ok);
        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Put, _handler.Requests[1].Method);
        var putBody = _handler.RequestBodies[1];
        Assert.Contains("tag-a", putBody);
        Assert.Contains("tag-b", putBody);
    }

    [Fact]
    public async Task AddTagToTicketAsync_isIdempotentWhenTagAlreadyPresent()
    {
        _handler.RespondTo(HttpMethod.Get, "/api/v1/tickets/t1", HttpStatusCode.OK,
            "{\"_id\":\"t1\",\"tags\":[\"tag-a\"]}");

        var ok = await _sut.AddTagToTicketAsync("t1", "tag-a");

        Assert.True(ok);
        // Only the GET should have been made — no PUT follow-up
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task RemoveTagFromTicketAsync_removesMatchingId()
    {
        _handler.RespondTo(HttpMethod.Get, "/api/v1/tickets/t1", HttpStatusCode.OK,
            "{\"_id\":\"t1\",\"tags\":[\"tag-a\",\"tag-b\",\"tag-c\"]}");
        _handler.RespondTo(HttpMethod.Put, "/api/v1/tickets/t1", HttpStatusCode.OK);

        var ok = await _sut.RemoveTagFromTicketAsync("t1", "tag-b");

        Assert.True(ok);
        var putBody = _handler.RequestBodies[1];
        Assert.Contains("tag-a", putBody);
        Assert.Contains("tag-c", putBody);
        Assert.DoesNotContain("tag-b", putBody);
    }

    [Fact]
    public async Task RemoveTagFromTicketAsync_isNoOpWhenTagAbsent()
    {
        _handler.RespondTo(HttpMethod.Get, "/api/v1/tickets/t1", HttpStatusCode.OK,
            "{\"_id\":\"t1\",\"tags\":[\"tag-a\"]}");

        var ok = await _sut.RemoveTagFromTicketAsync("t1", "tag-zzz");

        Assert.True(ok);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task AddTagToTicketAsync_returnsFalseWhenGetFails()
    {
        _handler.RespondTo(HttpMethod.Get, "/api/v1/tickets/t1", HttpStatusCode.NotFound);

        var ok = await _sut.AddTagToTicketAsync("t1", "tag-a");

        Assert.False(ok);
    }

    // -----------------------------------------------------------------
    // Comments (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task AddCommentAsync_postsToV2TicketUidComments()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.AddCommentAsync("42", "owner1", "Hello world");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/comments", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"comment\":\"Hello world\"", LastBody);
        Assert.DoesNotContain("ownerId", LastBody);
    }

    [Fact]
    public async Task AddCommentAsync_returnsFalseOnEmptyComment()
    {
        var ok = await _sut.AddCommentAsync("42", "owner1", "  ");
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task AddCommentAsync_returnsFalseOnServerError()
    {
        _handler.SetDefault(HttpStatusCode.InternalServerError);
        var ok = await _sut.AddCommentAsync("42", "owner1", "test");
        Assert.False(ok);
    }

    // -----------------------------------------------------------------
    // Notes (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task AddNoteAsync_postsToV2TicketUidNotes()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.AddNoteAsync("42", "owner1", "Internal note");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/notes", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"note\":\"Internal note\"", LastBody);
        Assert.DoesNotContain("owner", LastBody);
        Assert.DoesNotContain("ticketid", LastBody);
    }

    [Fact]
    public async Task AddNoteAsync_returnsFalseOnEmptyNote()
    {
        var ok = await _sut.AddNoteAsync("42", "owner1", "");
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    // -----------------------------------------------------------------
    // Subscribe (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task SubscribeToTicketAsync_putsToV2TicketUidSubscribe()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.SubscribeToTicketAsync("42", true);

        Assert.True(ok);
        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/subscribe", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"subscribe\":true", LastBody);
        Assert.DoesNotContain("user", LastBody);
    }

    [Fact]
    public async Task SubscribeToTicketAsync_returnsFalseOnEmptyUid()
    {
        var ok = await _sut.SubscribeToTicketAsync("", true);
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    // -----------------------------------------------------------------
    // Overdue (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetOverdueTicketsAsync_callsV2Endpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"tickets\":[]}");
        await _sut.GetOverdueTicketsAsync();

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/overdue", LastRequest.RequestUri!.AbsolutePath);
    }

    // -----------------------------------------------------------------
    // Notifications (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetNotificationsAsync_callsV2Endpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"notifications\":[]}");
        await _sut.GetNotificationsAsync();

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/users/notifications", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetNotificationCountAsync_callsV2CountEndpoint()
    {
        // Set _authToken via reflection so the IsAuthenticated guard passes
        typeof(TrueDeskApiService)
            .GetField("_authToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_sut, "fake-token");
        _handler.SetDefault(HttpStatusCode.OK, "{\"success\":true,\"count\":5}");

        var count = await _sut.GetNotificationCountAsync();

        Assert.Equal(5, count);
        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/users/notifications/count", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetNotificationCountAsync_returnsZeroWhenNotAuthenticated()
    {
        var count = await _sut.GetNotificationCountAsync();
        Assert.Equal(0, count);
        Assert.Empty(_handler.Requests);
    }

    // -----------------------------------------------------------------
    // Stats (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetTicketStatsAsync_callsV2StatsWithTimespan()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"ticketCount\":10}");
        await _sut.GetTicketStatsAsync(60);

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/stats/60", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTicketStatsAsync_defaultsTo30Days()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{}");
        await _sut.GetTicketStatsAsync();
        Assert.Equal("/api/v2/tickets/stats/30", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTicketStatsForGroupAsync_callsV2GroupStats()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"ticketCount\":3}");
        await _sut.GetTicketStatsForGroupAsync("grp1");

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/stats/group/grp1", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTicketStatsForUserAsync_callsV2UserStats()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"ticketCount\":7}");
        await _sut.GetTicketStatsForUserAsync("usr1");

        Assert.Equal(HttpMethod.Get, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/stats/user/usr1", LastRequest.RequestUri!.AbsolutePath);
    }

    // -----------------------------------------------------------------
    // Checklist (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task AddChecklistItemAsync_postsToV2ChecklistEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.AddChecklistItemAsync("42", "Buy milk");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/checklist", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"title\":\"Buy milk\"", LastBody);
    }

    [Fact]
    public async Task AddChecklistItemAsync_returnsFalseOnEmptyTitle()
    {
        var ok = await _sut.AddChecklistItemAsync("42", "  ");
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task UpdateChecklistItemAsync_putsToV2ChecklistItemEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.UpdateChecklistItemAsync("42", "item1", completed: true);

        Assert.True(ok);
        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/checklist/item1", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"completed\":true", LastBody);
    }

    [Fact]
    public async Task UpdateChecklistItemAsync_sendsOnlyProvidedFields()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        await _sut.UpdateChecklistItemAsync("42", "item1", title: "Updated");

        Assert.Contains("\"title\":\"Updated\"", LastBody);
        Assert.DoesNotContain("completed", LastBody);
    }

    [Fact]
    public async Task DeleteChecklistItemAsync_deletesV2ChecklistItem()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.DeleteChecklistItemAsync("42", "item1");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Delete, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/42/checklist/item1", LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteChecklistItemAsync_returnsFalseOnEmptyId()
    {
        var ok = await _sut.DeleteChecklistItemAsync("42", "");
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    // -----------------------------------------------------------------
    // Batch operations (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task BatchDeleteTicketsAsync_sendsDeleteWithIds()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"success\":true,\"deleted\":2,\"failed\":0}");
        var (deleted, failed) = await _sut.BatchDeleteTicketsAsync(new[] { "id1", "id2" });

        Assert.Equal(2, deleted);
        Assert.Equal(0, failed);
        Assert.Equal(HttpMethod.Delete, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/batch", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"ids\"", LastBody);
        Assert.Contains("id1", LastBody);
    }

    [Fact]
    public async Task BatchDeleteTicketsAsync_returnsZerosOnEmptyInput()
    {
        var (deleted, failed) = await _sut.BatchDeleteTicketsAsync(Array.Empty<string>());
        Assert.Equal(0, deleted);
        Assert.Equal(0, failed);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task BatchUpdateTicketsAsync_sendsPutWithBatch()
    {
        _handler.SetDefault(HttpStatusCode.OK, "{\"success\":3,\"failed\":0}");
        var batch = new[]
        {
            new Dictionary<string, object?> { ["id"] = "t1", ["status"] = "s1" },
            new Dictionary<string, object?> { ["id"] = "t2", ["assignee"] = "u1" }
        };
        var (updated, failed) = await _sut.BatchUpdateTicketsAsync(batch);

        Assert.Equal(3, updated);
        Assert.Equal(0, failed);
        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/tickets/batch", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"batch\"", LastBody);
    }

    [Fact]
    public async Task BatchUpdateTicketsAsync_returnsFailureOnServerError()
    {
        _handler.SetDefault(HttpStatusCode.InternalServerError);
        var batch = new[] { new Dictionary<string, object?> { ["id"] = "t1" } };
        var (updated, failed) = await _sut.BatchUpdateTicketsAsync(batch);

        Assert.Equal(0, updated);
        Assert.Equal(1, failed);
    }

    // -----------------------------------------------------------------
    // Profile (v2)
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateProfileAsync_putsToV2ProfileEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.UpdateProfileAsync("Max Mustermann", "Helfer", "0123", "0170");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Put, LastRequest.Method);
        Assert.Equal("/api/v2/accounts/profile", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"fullname\":\"Max Mustermann\"", LastBody);
        Assert.Contains("\"title\":\"Helfer\"", LastBody);
    }

    [Fact]
    public async Task UpdateProfileAsync_returnsFalseOnEmptyName()
    {
        var ok = await _sut.UpdateProfileAsync("  ", null, null, null);
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task UpdatePasswordAsync_postsToV2PasswordEndpoint()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var ok = await _sut.UpdatePasswordAsync("old123", "new456", "new456");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, LastRequest.Method);
        Assert.Equal("/api/v2/accounts/profile/update-password", LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"currentPassword\":\"old123\"", LastBody);
        Assert.Contains("\"newPassword\":\"new456\"", LastBody);
    }

    [Fact]
    public async Task UpdatePasswordAsync_returnsFalseOnEmptyPassword()
    {
        var ok = await _sut.UpdatePasswordAsync("old", "", "");
        Assert.False(ok);
        Assert.Empty(_handler.Requests);
    }
}
