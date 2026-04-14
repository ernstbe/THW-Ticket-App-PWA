using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class CalendarTests
{
    // -----------------------------------------------------------------
    // ParseEvents
    // -----------------------------------------------------------------

    [Fact]
    public void ParseEvents_emptyEnvelope_returnsEmptyList()
    {
        var result = Calendar.ParseEvents("{\"events\":[]}");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseEvents_malformed_returnsEmptyList()
    {
        var result = Calendar.ParseEvents("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseEvents_missingEventsKey_returnsEmptyList()
    {
        var result = Calendar.ParseEvents("{}");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseEvents_ticketAndRecurring_bothDeserialize()
    {
        const string json = """
        {"events":[
            {"id":"ticket-1","title":"Einsatzbesprechung","start":"2026-05-10T08:00:00.000Z","type":"ticket-deadline","resourceUid":1042},
            {"id":"task-9","title":"Wartung","start":"2026-05-15T07:00:00.000Z","type":"recurring-task"}
        ]}
        """;

        // Clock = after both events, so only ticket is overdue
        var clock = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = Calendar.ParseEvents(json, clock);

        Assert.Equal(2, result.Count);
        Assert.Equal("ticket-1", result[0].Id);
        Assert.Equal("Einsatzbesprechung", result[0].Title);
        Assert.Equal("ticket-deadline", result[0].Type);
        Assert.Equal("1042", result[0].ResourceUid);
        Assert.True(result[0].IsOverdue);

        Assert.Equal("task-9", result[1].Id);
        Assert.Equal("recurring-task", result[1].Type);
        // Recurring tasks are never marked overdue even when start < now
        Assert.False(result[1].IsOverdue);
    }

    [Fact]
    public void ParseEvents_futureTicket_isNotOverdue()
    {
        const string json = """
        {"events":[{"id":"ticket-99","title":"zukünftig","start":"2030-01-01T00:00:00.000Z","type":"ticket-deadline","resourceUid":99}]}
        """;
        var clock = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = Calendar.ParseEvents(json, clock);

        Assert.Single(result);
        Assert.False(result[0].IsOverdue);
    }

    [Fact]
    public void ParseEvents_resourceUidAsString_isForwardedAsIs()
    {
        const string json = """
        {"events":[{"id":"ticket-1","title":"t","start":"2026-05-10T08:00:00.000Z","type":"ticket-deadline","resourceUid":"abc123"}]}
        """;
        var result = Calendar.ParseEvents(json);
        Assert.Single(result);
        Assert.Equal("abc123", result[0].ResourceUid);
    }

    [Fact]
    public void ParseEvents_missingResourceUid_isNull()
    {
        const string json = """
        {"events":[{"id":"task-1","title":"t","start":"2026-05-10T08:00:00.000Z","type":"recurring-task"}]}
        """;
        var result = Calendar.ParseEvents(json);
        Assert.Single(result);
        Assert.Null(result[0].ResourceUid);
    }

    // -----------------------------------------------------------------
    // GroupByDate
    // -----------------------------------------------------------------

    [Fact]
    public void GroupByDate_groupsByLocalDateAndSortsAscending()
    {
        var events = new List<Calendar.CalendarEventVm>
        {
            new() { Id = "a", Title = "A", Start = new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc), Type = "ticket-deadline" },
            new() { Id = "b", Title = "B", Start = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc), Type = "ticket-deadline" },
            new() { Id = "c", Title = "C", Start = new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), Type = "recurring-task" }
        };

        var groups = Calendar.GroupByDate(events).ToList();

        Assert.Equal(2, groups.Count);
        // Within each group, earliest first
        var day1 = groups[0].ToList();
        Assert.Equal("B", day1[0].Title);
        Assert.Equal("A", day1[1].Title);
        var day2 = groups[1].ToList();
        Assert.Single(day2);
        Assert.Equal("C", day2[0].Title);
    }
}
