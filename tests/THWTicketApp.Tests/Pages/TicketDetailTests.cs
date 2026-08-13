using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class TicketDetailTests
{
    // -----------------------------------------------------------------
    // DeriveIsSubscribed (#315 — subscription bell must reflect the
    // server's subscriber list, not just per-device localStorage)
    // -----------------------------------------------------------------

    [Fact]
    public void DeriveIsSubscribed_userInServerSubscribers_returnsTrue()
    {
        var ticket = new Ticket { Subscribers = [new Assignee { Id = "u1" }, new Assignee { Id = "u2" }] };
        Assert.True(TicketDetail.DeriveIsSubscribed(ticket, "u2", [], 1001));
    }

    [Fact]
    public void DeriveIsSubscribed_userNotInServerSubscribers_returnsFalse()
    {
        var ticket = new Ticket { Subscribers = [new Assignee { Id = "u1" }] };
        // Local cache says subscribed, but the server list — which is
        // authoritative once present — disagrees.
        Assert.False(TicketDetail.DeriveIsSubscribed(ticket, "u2", [1001], 1001));
    }

    [Fact]
    public void DeriveIsSubscribed_emptyServerSubscribers_fallsBackToLocalStorage()
    {
        var ticket = new Ticket { Subscribers = [] };
        Assert.True(TicketDetail.DeriveIsSubscribed(ticket, "u1", [1001], 1001));
        Assert.False(TicketDetail.DeriveIsSubscribed(ticket, "u1", [], 1001));
    }

    [Fact]
    public void DeriveIsSubscribed_nullTicket_fallsBackToLocalStorage()
    {
        Assert.True(TicketDetail.DeriveIsSubscribed(null, "u1", [1001], 1001));
    }

    [Fact]
    public void DeriveIsSubscribed_noCurrentUserId_returnsFalseEvenIfSubscribersPresent()
    {
        var ticket = new Ticket { Subscribers = [new Assignee { Id = "u1" }] };
        Assert.False(TicketDetail.DeriveIsSubscribed(ticket, null, [1001], 1001));
    }

    // -----------------------------------------------------------------
    // TruncateTemplate
    // -----------------------------------------------------------------

    [Fact]
    public void TruncateTemplate_shortString_returnsUnchanged()
    {
        Assert.Equal("Hello", TicketDetail.TruncateTemplate("Hello"));
    }

    [Fact]
    public void TruncateTemplate_exactly50Chars_returnsUnchanged()
    {
        var input = new string('x', 50);
        Assert.Equal(input, TicketDetail.TruncateTemplate(input));
    }

    [Fact]
    public void TruncateTemplate_51Chars_truncatesWithEllipsis()
    {
        var input = new string('x', 51);
        var result = TicketDetail.TruncateTemplate(input);
        Assert.Equal(50, result.Length);
        Assert.EndsWith("...", result);
        Assert.StartsWith(new string('x', 47), result);
    }

    [Fact]
    public void TruncateTemplate_longString_truncatesTo50()
    {
        var input = new string('a', 200);
        var result = TicketDetail.TruncateTemplate(input);
        Assert.Equal(50, result.Length);
        Assert.EndsWith("...", result);
    }

    // Time-tracking tests (FormatElapsed / FormatMinutes) wurden mit
    // der Funktion entfernt — siehe Bug-Report b.ernst 2026-05-19.
}
