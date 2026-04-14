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
}
