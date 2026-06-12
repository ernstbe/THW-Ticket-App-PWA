using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class TicketDueDateHelperTests
{
    // -----------------------------------------------------------------
    // No due date is never overdue
    // -----------------------------------------------------------------

    [Fact]
    public void IsOverdue_null_returnsFalse()
        => Assert.False(TicketDueDateHelper.IsOverdue(null));

    [Fact]
    public void IsOverdue_minValueSentinel_returnsFalse()
        => Assert.False(TicketDueDateHelper.IsOverdue(DateTime.MinValue));

    // -----------------------------------------------------------------
    // Day semantics relative to the real clock (local dates, so these
    // are stable in any machine time zone)
    // -----------------------------------------------------------------

    [Fact]
    public void IsOverdue_dueToday_isNotOverdue()
        => Assert.False(TicketDueDateHelper.IsOverdue(DateTime.Now));

    [Fact]
    public void IsOverdue_dueYesterday_isOverdue()
        => Assert.True(TicketDueDateHelper.IsOverdue(DateTime.Now.AddDays(-1)));

    [Fact]
    public void IsOverdue_dueTomorrow_isNotOverdue()
        => Assert.False(TicketDueDateHelper.IsOverdue(DateTime.Now.AddDays(1)));

    // -----------------------------------------------------------------
    // Fixed-date day semantics. Local kind keeps these deterministic in
    // any machine time zone (the helper round-trips Local unchanged).
    // -----------------------------------------------------------------

    [Fact]
    public void IsOverdue_midnightDueDate_notOverdueLaterOnTheDueDay()
    {
        // Due day stored as midnight. At noon on the due day it must NOT
        // be overdue — the old instant comparison (DueDate < Now) flagged
        // it as soon as the clock passed the stored midnight.
        var due = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Local);
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Local);

        Assert.False(TicketDueDateHelper.IsOverdue(due, now));
    }

    [Fact]
    public void IsOverdue_midnightDueDate_overdueTheDayAfter()
    {
        var due = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Local);
        var now = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Local);

        Assert.True(TicketDueDateHelper.IsOverdue(due, now));
    }

    [Fact]
    public void IsOverdue_unspecifiedKind_isTreatedAsUtc()
    {
        // trudesk JSON parses into Unspecified kind — must behave exactly
        // like the UTC equivalent (same convention as ToLocalDisplayString).
        var dueUnspecified = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var dueUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            TicketDueDateHelper.IsOverdue(dueUtc, now),
            TicketDueDateHelper.IsOverdue(dueUnspecified, now));
    }

    [Fact]
    public void IsOverdue_matchesDisplayedLocalDate()
    {
        // Invariant in every time zone: a ticket is overdue exactly when
        // the date the app DISPLAYS (local date) lies before today.
        var due = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var displayedDate = due.ToLocalTime().Date;

        Assert.Equal(displayedDate < DateTime.Today, TicketDueDateHelper.IsOverdue(due));
    }
}
