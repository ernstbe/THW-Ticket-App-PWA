using THWTicketApp.Shared.Models;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Components;
using THWTicketApp.Web.Services;

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
    public void BuildFeed_carriesAuthorNameAndImageFromOwners()
    {
        var t = new Ticket
        {
            Comments = new List<Comment>
            {
                new() { Date = DateTime.Now, Text = "c", Owner = new Assignee { Fullname = "Hans Müller", Image = "hans.jpg" } }
            },
            Notes = new List<Note>
            {
                new() { Date = DateTime.Now.AddMinutes(-1), Content = "n", Owner = new Owner { Fullname = "Eva Beispiel", Image = "eva.png" } }
            }
        };

        var feed = TicketActivityFeed.BuildFeed(t);
        var comment = feed.Single(e => e.Kind == TicketActivityFeed.ActivityKind.Comment);
        var note = feed.Single(e => e.Kind == TicketActivityFeed.ActivityKind.Note);

        Assert.Equal("Hans Müller", comment.AuthorName);
        Assert.Equal("hans.jpg", comment.AuthorImage);
        Assert.Equal("Eva Beispiel", note.AuthorName);
        Assert.Equal("eva.png", note.AuthorImage);
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

    private static TicketActivityFeed BuildComponent(string language)
    {
        var localization = new LocalizationService(new InMemoryLocalStorageService());
        if (language != "de")
            localization.SetLanguageAsync(language).GetAwaiter().GetResult();
        return new TicketActivityFeed { L = localization };
    }

    [Theory]
    [InlineData(30, "gerade eben")]                    // <1 min
    [InlineData(60 * 5, "5 Min. her")]                 // 5 min
    [InlineData(60 * 60 * 3, "3 Std. her")]            // 3 hours
    [InlineData(60 * 60 * 24 * 2, "2 Tage her")]       // 2 days
    public void FormatRelative_german_picksReadableUnit(int secondsAgo, string expected)
    {
        var feed = BuildComponent("de");
        var when = DateTime.Now.AddSeconds(-secondsAgo);
        Assert.Equal(expected, feed.FormatRelative(when));
    }

    [Theory]
    [InlineData(30, "just now")]                        // <1 min
    [InlineData(60 * 5, "5 min ago")]                  // 5 min
    [InlineData(60 * 60 * 3, "3 h ago")]               // 3 hours
    [InlineData(60 * 60 * 24 * 2, "2 d ago")]          // 2 days
    public void FormatRelative_english_picksReadableUnit(int secondsAgo, string expected)
    {
        var feed = BuildComponent("en");
        var when = DateTime.Now.AddSeconds(-secondsAgo);
        Assert.Equal(expected, feed.FormatRelative(when));
    }

    [Fact]
    public void FormatRelative_olderThanWeek_fallsBackToAbsoluteDate()
    {
        var feed = BuildComponent("de");
        var when = DateTime.Now.AddDays(-30);
        var result = feed.FormatRelative(when);
        Assert.DoesNotContain("her", result);
        Assert.Contains(when.Year.ToString(), result);
    }
}
