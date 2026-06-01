using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// trudesk pre-PR-100 did not stamp the `updated` field; the PWA's
/// computed property must fall back to the creation date so older
/// tickets keep their place in "Zuletzt aktualisiert" sorting and
/// don't render as 01.01.0001 in the UI.
/// </summary>
public class TicketLastActivityAtTests
{
    [Fact]
    public void UpdatedSet_returnsUpdated()
    {
        var ticket = new Ticket
        {
            Date = new DateTime(2026, 5, 1),
            Updated = new DateTime(2026, 5, 20)
        };
        Assert.Equal(new DateTime(2026, 5, 20), ticket.LastActivityAt);
    }

    [Fact]
    public void UpdatedUnset_fallsBackToDate()
    {
        var ticket = new Ticket
        {
            Date = new DateTime(2026, 5, 1)
            // Updated left at default = DateTime.MinValue
        };
        Assert.Equal(new DateTime(2026, 5, 1), ticket.LastActivityAt);
    }

    [Fact]
    public void BothUnset_returnsMinValue()
    {
        var ticket = new Ticket();
        Assert.Equal(DateTime.MinValue, ticket.LastActivityAt);
    }
}
