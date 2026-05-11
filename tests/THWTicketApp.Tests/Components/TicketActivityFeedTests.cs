using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Components;

namespace THWTicketApp.Tests.Components;

/// <summary>
/// Tests for the activity feed merge logic — comments + notes + history
/// have to interleave by timestamp with newest first.
/// </summary>
public class TicketActivityFeedTests
{
    [Fact]
    public void BuildFeed_nullTicket_returnsEmpty()
    {
        Assert.Empty(TicketActivityFeed.BuildFeed(null));
    }

    [Fact]
    public void BuildFeed_emptyTicket_returnsEmpty()
    {
        var t = new Ticket();
        Assert.Empty(TicketActivityFeed.BuildFeed(t));
    }

    [Fact]
    public void BuildFeed_mergesCommentsNotesAndHistoryInTimeOrder()
    {
        var t = new Ticket
        {
            Comments = new List<Comment>
            {
                new() { Date = new DateTime(2026, 1, 1, 10, 0, 0), Text = "first comment" },
                new() { Date = new DateTime(2026, 1, 1, 14, 0, 0), Text = "third event" }
            },
            Notes = new List<Note>
            {
                new() { Date = new DateTime(2026, 1, 1, 12, 0, 0), Content = "second event" }
            },
            History = new List<HistoryItem>
            {
                new() { Date = new DateTime(2026, 1, 1, 16, 0, 0), Action = "fourth" }
            }
        };

        var feed = TicketActivityFeed.BuildFeed(t);

        // Newest first
        Assert.Equal(4, feed.Count);
        Assert.Equal(new DateTime(2026, 1, 1, 16, 0, 0), feed[0].Date);
        Assert.Equal(new DateTime(2026, 1, 1, 14, 0, 0), feed[1].Date);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), feed[2].Date);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0), feed[3].Date);
    }

    [Fact]
    public void BuildFeed_tagsEventsWithCorrectKind()
    {
        var t = new Ticket
        {
            Comments = new List<Comment> { new() { Date = DateTime.Now.AddMinutes(-1), Text = "c" } },
            Notes = new List<Note> { new() { Date = DateTime.Now.AddMinutes(-2), Content = "n" } },
            History = new List<HistoryItem> { new() { Date = DateTime.Now.AddMinutes(-3), Action = "h" } }
        };

        var feed = TicketActivityFeed.BuildFeed(t);
        Assert.Equal(TicketActivityFeed.ActivityKind.Comment, feed[0].Kind);
        Assert.Equal(TicketActivityFeed.ActivityKind.Note, feed[1].Kind);
        Assert.Equal(TicketActivityFeed.ActivityKind.History, feed[2].Kind);
    }

    [Theory]
    [InlineData(30, "gerade eben")]                    // <1 min
    [InlineData(60 * 5, "vor 5 Min.")]                 // 5 min
    [InlineData(60 * 60 * 3, "vor 3 Std.")]            // 3 hours
    [InlineData(60 * 60 * 24 * 2, "vor 2 Tagen")]      // 2 days
    public void FormatRelative_picksReadableUnit(int secondsAgo, string expected)
    {
        var when = DateTime.Now.AddSeconds(-secondsAgo);
        Assert.Equal(expected, TicketActivityFeed.FormatRelative(when));
    }

    [Fact]
    public void FormatRelative_olderThanWeek_fallsBackToAbsoluteDate()
    {
        var when = DateTime.Now.AddDays(-30);
        var result = TicketActivityFeed.FormatRelative(when);
        Assert.DoesNotContain("vor", result);
        Assert.Contains(when.Year.ToString(), result);
    }
}
