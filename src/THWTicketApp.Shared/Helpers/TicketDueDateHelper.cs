namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Central overdue semantics for ticket due dates: a ticket is overdue only
/// once its due DAY is over in local time — not from the stored instant.
/// trudesk stores dueDate as midnight UTC, so an instant comparison
/// (DueDate &lt; DateTime.Now) would flag a ticket as overdue on its due day
/// already at ~01:00/02:00 local (DE). This mirrors trudesk's
/// deadlineHelper day comparison and the local-date display used by
/// <see cref="DateTimeHelper.ToLocalDisplayString(DateTime, string)"/>.
/// </summary>
public static class TicketDueDateHelper
{
    /// <summary>
    /// True when the due day (local date) lies before today. No due date
    /// (null or default/MinValue sentinel) is never overdue.
    /// </summary>
    public static bool IsOverdue(DateTime? dueDate) => IsOverdue(dueDate, DateTime.Now);

    /// <summary>
    /// Overload with an explicit reference instant (testable, or "was it
    /// overdue when it was closed?"). <paramref name="now"/> follows the
    /// same kind handling as the due date: Unspecified is assumed UTC and
    /// both sides are compared as local dates.
    /// </summary>
    public static bool IsOverdue(DateTime? dueDate, DateTime now)
    {
        if (dueDate is null || dueDate.Value == default) return false;
        return ToLocalDate(dueDate.Value) < ToLocalDate(now);
    }

    // Same kind handling as DateTimeHelper.ToLocalDisplayString: trudesk
    // timestamps arrive as UTC; Unspecified is assumed UTC.
    private static DateTime ToLocalDate(DateTime dt)
    {
        var utc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
        return utc.ToLocalTime().Date;
    }
}
