using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Models;

public class UserTicketStatsTests
{
    // Regression for frontend-review BUG-3: the v2 /tickets/stats/assignee/:user
    // endpoint returns the stats FLAT (sendApiSuccess merges them onto the root),
    // NOT under a "data" wrapper. Reading a nested object left the Team page at 0.
    [Fact]
    public void Deserializes_flat_v2_assignee_stats_shape()
    {
        const string json =
            "{\"success\":true,\"ticketCount\":31,\"closedCount\":12,\"avgResponse\":4.5," +
            "\"recentTickets\":[]}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var stats = JsonSerializer.Deserialize<UserTicketStats>(json, options);

        Assert.NotNull(stats);
        Assert.True(stats!.Success);
        Assert.Equal(31, stats.TicketCount);
        Assert.Equal(12, stats.ClosedCount);
        Assert.Equal(4.5, stats.AvgResponse);
    }
}
