using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Helpers;

public class StatsParserTests
{
    // --- ParseOverview ---

    [Fact]
    public void ParseOverview_V2Shape_ParsesCountsAndGraphData()
    {
        var json = """
        {
            "success": true,
            "timespan": 30,
            "graphData": [
                { "date": "2026-06-10", "value": 3 },
                { "date": "2026-06-11", "value": 0 },
                { "date": "2026-06-12", "value": 5 }
            ],
            "ticketCount": 42,
            "closedCount": 17,
            "ticketAvg": 6.5,
            "mostAssignee": { "name": "Max Mustermann", "value": 12 },
            "mostRequester": { "fullname": "Erika Musterfrau" }
        }
        """;

        var result = StatsParser.ParseOverview(json);

        Assert.Equal(42, result.TicketCount);
        Assert.Equal(17, result.ClosedCount);
        Assert.Equal(6.5, result.AvgResponseHours);
        Assert.Equal("Max Mustermann", result.MostAssignee);
        Assert.Equal("Erika Musterfrau", result.MostRequester);
        Assert.Equal(3, result.GraphData.Count);
        Assert.Equal(new DateOnly(2026, 6, 12), result.GraphData[2].Date);
        Assert.Equal(5, result.GraphData[2].Count);
    }

    [Fact]
    public void ParseOverview_V1Shape_ReadsGraphPointsFromDataProperty()
    {
        var json = """
        {
            "data": [ { "date": "2026-06-01", "value": 2 } ],
            "ticketCount": 7,
            "closedCount": 1
        }
        """;

        var result = StatsParser.ParseOverview(json);

        Assert.Equal(7, result.TicketCount);
        Assert.Single(result.GraphData);
        Assert.Equal(2, result.GraphData[0].Count);
    }

    [Fact]
    public void ParseOverview_CacheStillWarmingUp_MissingFieldsAreNull()
    {
        // Right after a backend restart the cache keys are unset and the
        // counters are simply absent from the response.
        var json = """{ "success": true, "timespan": 30 }""";

        var result = StatsParser.ParseOverview(json);

        Assert.Null(result.TicketCount);
        Assert.Null(result.ClosedCount);
        Assert.Null(result.AvgResponseHours);
        Assert.Empty(result.GraphData);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    public void ParseOverview_MalformedInput_ReturnsEmptyStats(string? json)
    {
        var result = StatsParser.ParseOverview(json);

        Assert.Null(result.TicketCount);
        Assert.Empty(result.GraphData);
    }

    [Fact]
    public void ParseOverview_SkipsGraphPointsWithBadDates()
    {
        var json = """
        {
            "graphData": [
                { "date": "garbage", "value": 1 },
                { "date": "2026-06-12", "value": 4 },
                { "value": 9 }
            ]
        }
        """;

        var result = StatsParser.ParseOverview(json);

        Assert.Single(result.GraphData);
        Assert.Equal(4, result.GraphData[0].Count);
    }

    // --- ParseAssigneeStats ---

    [Fact]
    public void ParseAssigneeStats_RootLevelCounters_Parses()
    {
        var json = """{ "success": true, "ticketCount": 10, "closedCount": 4, "avgResponse": 2.5 }""";

        var result = StatsParser.ParseAssigneeStats(json);

        Assert.Equal(10, result.TicketCount);
        Assert.Equal(4, result.ClosedCount);
        Assert.Equal(2.5, result.AvgResponseHours);
        Assert.Equal(6, result.OpenCount);
    }

    [Fact]
    public void ParseAssigneeStats_DataWrapper_Parses()
    {
        var json = """{ "success": true, "data": { "ticketCount": 3, "closedCount": 5 } }""";

        var result = StatsParser.ParseAssigneeStats(json);

        Assert.Equal(3, result.TicketCount);
        Assert.Equal(5, result.ClosedCount);
        // More closed than assigned must never yield a negative open count.
        Assert.Equal(0, result.OpenCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ broken")]
    public void ParseAssigneeStats_MalformedInput_ReturnsZeroes(string? json)
    {
        var result = StatsParser.ParseAssigneeStats(json);

        Assert.Equal(0, result.TicketCount);
        Assert.Equal(0, result.ClosedCount);
        Assert.Equal(0, result.OpenCount);
    }

    // --- ParseWorkload ---

    [Fact]
    public void ParseWorkload_ParsesRowsInServerOrder()
    {
        var json = """
        { "success": true, "workload": [
            { "id": "a", "name": "Anna Admin", "ticketCount": 8, "closedCount": 3, "avgResponse": 2.0 },
            { "id": "b", "name": "Bert Boss", "ticketCount": 2, "closedCount": 2, "avgResponse": 0 }
        ] }
        """;

        var result = StatsParser.ParseWorkload(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("Anna Admin", result[0].Name);
        Assert.Equal(8, result[0].Stats.TicketCount);
        Assert.Equal(3, result[0].Stats.ClosedCount);
        Assert.Equal(5, result[0].Stats.OpenCount);
        Assert.Equal(2.0, result[0].Stats.AvgResponseHours);
        Assert.Equal("Bert Boss", result[1].Name);
        Assert.Equal(0, result[1].Stats.OpenCount);
    }

    [Fact]
    public void ParseWorkload_MissingName_FallsBackToPlaceholder()
    {
        var json = """{ "workload": [ { "id": "x", "ticketCount": 1, "closedCount": 0 } ] }""";

        var result = StatsParser.ParseWorkload(json);

        Assert.Single(result);
        Assert.Equal("?", result[0].Name);
        Assert.Equal(1, result[0].Stats.TicketCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ broken")]
    [InlineData("""{ "success": true }""")]
    public void ParseWorkload_MalformedOrMissing_ReturnsEmptyList(string? json)
    {
        Assert.Empty(StatsParser.ParseWorkload(json));
    }

    // --- BuildVolumeSeries ---

    [Fact]
    public void BuildVolumeSeries_EmptyInput_ReturnsEmptyArrays()
    {
        var (labels, values) = StatsParser.BuildVolumeSeries([], 30);

        Assert.Empty(labels);
        Assert.Empty(values);
    }

    [Fact]
    public void BuildVolumeSeries_30Days_BucketsByWeek()
    {
        var start = new DateOnly(2026, 5, 14);
        var points = Enumerable.Range(0, 30)
            .Select(i => new StatsGraphPoint(start.AddDays(i), 1))
            .ToList();

        var (labels, values) = StatsParser.BuildVolumeSeries(points, 30);

        // 30 daily points → 4 full weeks + 1 partial week.
        Assert.Equal(5, labels.Length);
        Assert.Equal(5, values.Length);
        Assert.Equal("14.05.", labels[0]);
        Assert.Equal(7, values[0]);
        Assert.Equal(2, values[4]);
        Assert.Equal(30, values.Sum());
    }

    [Fact]
    public void BuildVolumeSeries_365Days_BucketsByMonth()
    {
        var points = new List<StatsGraphPoint>
        {
            new(new DateOnly(2025, 12, 30), 2),
            new(new DateOnly(2026, 1, 5), 3),
            new(new DateOnly(2026, 1, 20), 4)
        };

        var (labels, values) = StatsParser.BuildVolumeSeries(points, 365);

        Assert.Equal(["12.25", "01.26"], labels);
        Assert.Equal([2d, 7d], values);
    }

    [Fact]
    public void BuildVolumeSeries_UnorderedInput_IsSortedBeforeBucketing()
    {
        var points = new List<StatsGraphPoint>
        {
            new(new DateOnly(2026, 6, 12), 1),
            new(new DateOnly(2026, 6, 1), 2)
        };

        var (labels, values) = StatsParser.BuildVolumeSeries(points, 30);

        Assert.Equal("01.06.", labels[0]);
        Assert.Equal(2, values[0]);
    }

    // --- ComputeStatusDistribution ---

    [Fact]
    public void ComputeStatusDistribution_GroupsByStatusOrderedByCountDesc()
    {
        var tickets = new[]
        {
            MakeTicket("Offen", "#29B955", false),
            MakeTicket("Offen", "#29B955", false),
            MakeTicket("Erledigt", "#CCCCCC", true),
            new Ticket { Status = null }
        };

        var result = StatsParser.ComputeStatusDistribution(tickets, "Unbekannt");

        Assert.Equal(3, result.Count);
        Assert.Equal("Offen", result[0].Name);
        Assert.Equal(2, result[0].Count);
        Assert.Equal("#29B955", result[0].Color);
        Assert.Contains(result, s => s.Name == "Unbekannt" && s.Color == "#9E9E9E");
    }

    [Fact]
    public void ComputeStatusDistribution_IgnoresDeletedTickets()
    {
        var deleted = MakeTicket("Offen", "#29B955", false);
        deleted.Deleted = true;

        var result = StatsParser.ComputeStatusDistribution([deleted], "Unbekannt");

        Assert.Empty(result);
    }

    // --- KPI counters ---

    [Fact]
    public void CountOpen_CountsOnlyUnresolvedNonDeleted()
    {
        var resolved = MakeTicket("Erledigt", "#CCC", isResolved: true);
        var open = MakeTicket("Offen", "#2A2", isResolved: false);
        var noStatus = new Ticket();
        var deleted = MakeTicket("Offen", "#2A2", isResolved: false);
        deleted.Deleted = true;

        Assert.Equal(2, StatsParser.CountOpen([resolved, open, noStatus, deleted]));
    }

    [Fact]
    public void CountCreatedAndClosedSince_RespectCutoff()
    {
        var cutoff = new DateTime(2026, 6, 8);
        var oldTicket = new Ticket { Date = new DateTime(2026, 5, 1) };
        var newOpen = new Ticket { Date = new DateTime(2026, 6, 10) };
        var newClosed = new Ticket { Date = new DateTime(2026, 6, 9), ClosedDate = new DateTime(2026, 6, 11) };
        var oldClosed = new Ticket { Date = new DateTime(2026, 5, 1), ClosedDate = new DateTime(2026, 5, 2) };

        Ticket[] all = [oldTicket, newOpen, newClosed, oldClosed];

        Assert.Equal(2, StatsParser.CountCreatedSince(all, cutoff));
        Assert.Equal(1, StatsParser.CountClosedSince(all, cutoff));
    }

    private static Ticket MakeTicket(string statusName, string color, bool isResolved) => new()
    {
        Status = new Status { Name = statusName, HtmlColor = color, IsResolved = isResolved }
    };
}
