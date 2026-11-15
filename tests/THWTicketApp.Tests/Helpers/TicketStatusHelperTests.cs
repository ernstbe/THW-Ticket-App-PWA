using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Helpers;

public class TicketStatusHelperTests
{
    private static Status S(string? name, bool isResolved = false, bool isInProgress = false)
        => new() { Name = name, IsResolved = isResolved, IsInProgress = isInProgress };

    [Theory]
    [InlineData("Offen")]
    [InlineData("Neu")]
    [InlineData("In Bearbeitung")]
    [InlineData("Ausstehend")]
    [InlineData("Wartend")]
    [InlineData("Open")]
    [InlineData("New")]
    public void KnownActiveStatus_isActive_evenIfIsResolvedMisconfigured(string name)
    {
        // Regression for frontend-review BUG-2: a mis-set isResolved=true on a
        // clearly-active status must NOT hide it from the "Aktiv" filter.
        var status = S(name, isResolved: true);
        Assert.True(TicketStatusHelper.IsActive(status));
        Assert.False(TicketStatusHelper.IsClosed(status));
    }

    [Theory]
    [InlineData("Geschlossen")]
    [InlineData("Closed")]
    [InlineData("Gelöst")]
    [InlineData("Resolved")]
    public void ClosedNamedStatus_isClosed(string name)
    {
        Assert.True(TicketStatusHelper.IsClosed(S(name)));
        Assert.False(TicketStatusHelper.IsActive(S(name)));
    }

    [Fact]
    public void UnknownCustomStatus_fallsBackToFlags()
    {
        Assert.True(TicketStatusHelper.IsClosed(S("Archiviert", isResolved: true)));
        Assert.False(TicketStatusHelper.IsClosed(S("Eskaliert", isResolved: false)));
        // In-progress custom status is never closed.
        Assert.False(TicketStatusHelper.IsClosed(S("Eskaliert", isResolved: true, isInProgress: true)));
    }

    [Fact]
    public void NullStatus_isNotClosed()
    {
        Assert.False(TicketStatusHelper.IsClosed(null));
        Assert.True(TicketStatusHelper.IsActive(null));
    }

    [Fact]
    public void IsOpen_onlyForOpenNewBucket()
    {
        Assert.True(TicketStatusHelper.IsOpen(S("Offen")));
        Assert.True(TicketStatusHelper.IsOpen(S("Neu")));
        Assert.False(TicketStatusHelper.IsOpen(S("In Bearbeitung")));
        Assert.False(TicketStatusHelper.IsOpen(S("Geschlossen")));
    }

    private static Ticket T(int uid, string status, int minutesAgo, bool deleted = false) => new()
    {
        Uid = uid,
        Status = S(status),
        Updated = DateTime.UtcNow.AddMinutes(-minutesAgo),
        Deleted = deleted
    };

    [Fact]
    public void MostRecentActive_skipsClosedAndDeleted_newestFirst()
    {
        var tickets = new[]
        {
            T(1, "Geschlossen", 1),
            T(2, "Offen", 5),
            T(3, "In Bearbeitung", 2),
            T(4, "Gelöst", 0),
            T(5, "Offen", 3, deleted: true),
            T(6, "Wartend", 10)
        };

        var recent = TicketStatusHelper.MostRecentActive(tickets, 5);

        Assert.Equal(new[] { 3, 2, 6 }, recent.Select(t => t.Uid));
    }

    [Fact]
    public void MostRecentActive_respectsCount()
    {
        var tickets = Enumerable.Range(1, 8).Select(i => T(i, "Offen", i)).ToList();

        var recent = TicketStatusHelper.MostRecentActive(tickets, 5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, recent.Select(t => t.Uid));
    }
}
