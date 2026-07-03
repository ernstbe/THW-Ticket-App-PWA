using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class TicketStats
{
    public int TicketCount { get; set; }
    public int ClosedCount { get; set; }
    public double TicketAvg { get; set; }
    public TicketStatItem? MostRequester { get; set; }
    public TicketStatItem? MostCommenter { get; set; }
    public TicketStatItem? MostAssignee { get; set; }
    public TicketStatItem? MostActiveTicket { get; set; }
    public DateTime? LastUpdated { get; set; }
    public List<TicketStatGraphData> Data { get; set; } = [];
}

public class TicketStatItem
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Fullname { get; set; }
    public int Count { get; set; }
    public int Uid { get; set; }
    public string? Subject { get; set; }
}

public class TicketStatGraphData
{
    [JsonPropertyName("_id")]
    public TicketStatGraphId? Id { get; set; }
    public int Count { get; set; }
}

public class TicketStatGraphId
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
}

public class GroupTicketStats
{
    public bool Success { get; set; }
    public GroupTicketStatsData? Data { get; set; }
}

public class GroupTicketStatsData
{
    public int TicketCount { get; set; }
    public int ClosedCount { get; set; }
    public double AvgResponse { get; set; }
    public List<TicketStatGraphData> GraphData { get; set; } = [];
    public List<TagCount> Tags { get; set; } = [];
}

// The v2 /tickets/stats/assignee/:user (and /user/:user) endpoints return the
// stats FLAT — sendApiSuccess does Object.assign({ success: true }, { ticketCount,
// closedCount, avgResponse, ... }). These fields are therefore top-level, NOT
// under a "data" wrapper; reading them from a nested object made the Team page
// show 0 for every member (frontend review BUG-3).
public class UserTicketStats
{
    public bool Success { get; set; }
    public int TicketCount { get; set; }
    public int ClosedCount { get; set; }
    public double AvgResponse { get; set; }
}

public class TagCount
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int Count { get; set; }
}
