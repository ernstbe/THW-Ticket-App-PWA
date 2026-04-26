using MudBlazor;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class SettingsTests
{
    [Theory]
    [InlineData("2026-04-26T14:30:00.0000000Z")]
    [InlineData("2026-01-01T00:00:00Z")]
    public void FormatTimestamp_validIso_returnsLocalTimeString(string iso)
    {
        var result = Settings.FormatTimestamp(iso);
        // Should contain date and time parts, not the raw ISO string
        Assert.Contains("2026", result);
        Assert.DoesNotContain("T", result);
        Assert.DoesNotContain("Z", result);
    }

    [Fact]
    public void FormatTimestamp_invalidString_returnsInputAsIs()
    {
        var result = Settings.FormatTimestamp("not-a-date");
        Assert.Equal("not-a-date", result);
    }

    [Fact]
    public void FormatTimestamp_emptyString_returnsEmpty()
    {
        var result = Settings.FormatTimestamp("");
        Assert.Equal("", result);
    }

    [Fact]
    public void GetLevelColor_error_returnsError()
    {
        Assert.Equal(Color.Error, Settings.GetLevelColor("error"));
    }

    [Fact]
    public void GetLevelColor_warn_returnsWarning()
    {
        Assert.Equal(Color.Warning, Settings.GetLevelColor("warn"));
    }

    [Fact]
    public void GetLevelColor_info_returnsDefault()
    {
        Assert.Equal(Color.Default, Settings.GetLevelColor("info"));
    }

    [Fact]
    public void GetLevelColor_unknown_returnsDefault()
    {
        Assert.Equal(Color.Default, Settings.GetLevelColor("debug"));
    }
}
