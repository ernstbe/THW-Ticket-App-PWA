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

    // -----------------------------------------------------------------
    // FormatElapsed
    // -----------------------------------------------------------------

    [Fact]
    public void FormatElapsed_zero_returnsZeroPadded()
    {
        Assert.Equal("00:00:00", TicketDetail.FormatElapsed(TimeSpan.Zero));
    }

    [Fact]
    public void FormatElapsed_standardDuration_formatsCorrectly()
    {
        var ts = new TimeSpan(1, 30, 45);
        Assert.Equal("01:30:45", TicketDetail.FormatElapsed(ts));
    }

    [Fact]
    public void FormatElapsed_overOneDay_showsTotalHours()
    {
        var ts = new TimeSpan(1, 2, 15, 30); // 1 day, 2h, 15m, 30s = 26h
        Assert.Equal("26:15:30", TicketDetail.FormatElapsed(ts));
    }

    [Fact]
    public void FormatElapsed_secondsOnly_padsCorrectly()
    {
        var ts = TimeSpan.FromSeconds(5);
        Assert.Equal("00:00:05", TicketDetail.FormatElapsed(ts));
    }

    // -----------------------------------------------------------------
    // FormatMinutes
    // -----------------------------------------------------------------

    [Fact]
    public void FormatMinutes_zero_returnsZeroMinutes()
    {
        Assert.Equal("0m", TicketDetail.FormatMinutes(0));
    }

    [Fact]
    public void FormatMinutes_under60_returnsMinutesOnly()
    {
        Assert.Equal("45m", TicketDetail.FormatMinutes(45));
    }

    [Fact]
    public void FormatMinutes_exactly60_returns1Hour()
    {
        Assert.Equal("1h 0m", TicketDetail.FormatMinutes(60));
    }

    [Fact]
    public void FormatMinutes_mixed_returnsHoursAndMinutes()
    {
        Assert.Equal("2h 30m", TicketDetail.FormatMinutes(150));
    }

    [Fact]
    public void FormatMinutes_exactHours_showsZeroMinutes()
    {
        Assert.Equal("3h 0m", TicketDetail.FormatMinutes(180));
    }
}
