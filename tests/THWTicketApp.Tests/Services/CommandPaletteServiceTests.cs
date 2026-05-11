using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Tests for the command palette state machine + the scoring used to
/// rank static actions against a query.
/// </summary>
public class CommandPaletteServiceTests
{
    private static PaletteAction Make(string id, string title, string? subtitle = null, string[]? keywords = null) =>
        new() { Id = id, Title = title, Subtitle = subtitle, Keywords = keywords };

    [Fact]
    public void Open_setsIsOpenAndFiresStateChanged()
    {
        var sut = new CommandPaletteService();
        int fired = 0;
        sut.StateChanged += () => fired++;

        sut.Open();

        Assert.True(sut.IsOpen);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Open_isIdempotent()
    {
        var sut = new CommandPaletteService();
        int fired = 0;
        sut.StateChanged += () => fired++;

        sut.Open();
        sut.Open();

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Close_clearsQuery()
    {
        var sut = new CommandPaletteService();
        sut.Open();
        sut.SetQuery("ticket");

        sut.Close();

        Assert.False(sut.IsOpen);
        Assert.Equal(string.Empty, sut.Query);
    }

    [Fact]
    public void Toggle_alternates()
    {
        var sut = new CommandPaletteService();
        sut.Toggle();
        Assert.True(sut.IsOpen);
        sut.Toggle();
        Assert.False(sut.IsOpen);
    }

    [Fact]
    public void Register_deduplicatesById()
    {
        // Hot-reload or re-init shouldn't grow the list — registering an
        // action with the same Id replaces the previous one.
        var sut = new CommandPaletteService();
        sut.Register(Make("nav-tickets", "Tickets v1"));
        sut.Register(Make("nav-tickets", "Tickets v2"));

        Assert.Single(sut.StaticActions);
        Assert.Equal("Tickets v2", sut.StaticActions[0].Title);
    }

    [Fact]
    public void RegisterMany_addsAll()
    {
        var sut = new CommandPaletteService();
        sut.RegisterMany(new[] { Make("a", "A"), Make("b", "B"), Make("c", "C") });

        Assert.Equal(3, sut.StaticActions.Count);
    }

    [Theory]
    [InlineData("", 1)]                       // empty query matches everything
    [InlineData("ticket", 100)]               // prefix match wins
    [InlineData("Ticket", 100)]               // case-insensitive
    [InlineData("ick", 50)]                   // substring match
    [InlineData("xyz", 0)]                    // no match
    public void MatchScore_prefersTitlePrefix(string query, int expectedAtLeast)
    {
        var a = Make("a", "Tickets");
        var score = a.MatchScore(query);
        Assert.True(score >= expectedAtLeast,
            $"Expected score >= {expectedAtLeast} for '{query}', got {score}");
        if (expectedAtLeast == 0) Assert.Equal(0, score);
    }

    [Fact]
    public void MatchScore_keywordsMatchToo()
    {
        var a = Make("a", "Tickets", keywords: new[] { "liste", "list" });
        Assert.True(a.MatchScore("liste") > 0);
        Assert.True(a.MatchScore("list") > 0);
    }

    [Fact]
    public void MatchScore_titlePrefixBeatsKeywordPrefix()
    {
        // Sanity: title should generally rank higher than keyword.
        var a = Make("a", "Tickets", keywords: new[] { "search" });
        var titleScore = a.MatchScore("Tick");
        var keywordScore = a.MatchScore("sea");
        Assert.True(titleScore > keywordScore);
    }
}
