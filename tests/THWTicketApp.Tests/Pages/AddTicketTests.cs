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

    [Fact]
    public void ParseTemplates_validJson_returnsList()
    {
        var json = """{"ticketTemplates":[{"_id":"1","name":"Einsatz","subject":"Einsatz vom {date}","issue":"Bericht..."}]}""";
        var result = AddTicket.ParseTemplates(json);

        Assert.Single(result);
        Assert.Equal("Einsatz", result[0].Name);
        Assert.Equal("Einsatz vom {date}", result[0].Subject);
        Assert.Equal("Bericht...", result[0].Issue);
    }

    [Fact]
    public void ParseTemplates_emptyArray_returnsEmpty()
    {
        var json = """{"ticketTemplates":[]}""";
        var result = AddTicket.ParseTemplates(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_missingKey_returnsEmpty()
    {
        var json = """{"other":[]}""";
        var result = AddTicket.ParseTemplates(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_invalidJson_returnsEmpty()
    {
        var result = AddTicket.ParseTemplates("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_checklist_extractsTitlesInOrder()
    {
        var json = """
        {"ticketTemplates":[{"name":"T","subject":"S","checklist":[{"_id":"c1","title":"Erst"},{"_id":"c2","title":"Dann"}]}]}
        """;
        var result = AddTicket.ParseTemplates(json);
        Assert.Equal(new[] { "Erst", "Dann" }, result[0].Checklist);
    }

    [Fact]
    public void ParseTemplates_missingChecklist_returnsEmptyList()
    {
        var json = """{"ticketTemplates":[{"name":"T","subject":"S"}]}""";
        var result = AddTicket.ParseTemplates(json);
        Assert.Empty(result[0].Checklist);
    }

    [Fact]
    public void ParseTemplates_multipleTemplates_parsesAll()
    {
        var json = """{"ticketTemplates":[{"name":"A","subject":"S1"},{"name":"B","subject":"S2","issue":"I2"}]}""";
        var result = AddTicket.ParseTemplates(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Null(result[0].Issue);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("I2", result[1].Issue);
    }
}
