using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class TicketDetailTests
{
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
