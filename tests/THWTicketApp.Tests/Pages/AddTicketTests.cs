using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class AddTicketTests
{
    [Fact]
    public void FilterGroupsByVisibility_nullVisible_returnsAllGroups()
    {
        // Profile lookup failed — fall back to the full list and let the
        // server-side check (trudesk PR #97) gate it.
        var raw = new List<Group>
        {
            new() { Id = "g1", Name = "One" },
            new() { Id = "g2", Name = "Two" },
        };

        var result = AddTicket.FilterGroupsByVisibility(raw, null);

        Assert.Same(raw, result);
    }

    [Fact]
    public void FilterGroupsByVisibility_emptyVisible_returnsEmpty()
    {
        // Profile loaded but the user has zero reachable groups — caller
        // uses this to surface the warning banner.
        var raw = new List<Group>
        {
            new() { Id = "g1", Name = "One" },
        };

        var result = AddTicket.FilterGroupsByVisibility(raw, new List<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void FilterGroupsByVisibility_filtersToVisibleIdsOnly()
    {
        var raw = new List<Group>
        {
            new() { Id = "g1", Name = "One" },
            new() { Id = "g2", Name = "Two" },
            new() { Id = "g3", Name = "Three" },
        };

        var result = AddTicket.FilterGroupsByVisibility(raw, new List<string> { "g1", "g3" });

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "g1", "g3" }, result.Select(g => g.Id).ToArray());
    }

    [Fact]
    public void FilterGroupsByVisibility_skipsGroupsWithNullId()
    {
        var raw = new List<Group>
        {
            new() { Id = null, Name = "Broken" },
            new() { Id = "g1", Name = "One" },
        };

        var result = AddTicket.FilterGroupsByVisibility(raw, new List<string> { "g1" });

        Assert.Single(result);
        Assert.Equal("g1", result[0].Id);
    }

    // Template parsing moved to the shared TicketTemplateParser —
    // see Helpers/TicketTemplateParserTests.
}
