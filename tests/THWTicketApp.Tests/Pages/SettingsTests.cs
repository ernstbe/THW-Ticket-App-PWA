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

// Added in R9.1

public class MainLayoutTests
{
    [Fact]
    public void LightenColor_standardHex_addsBrightness()
    {
        var result = THWTicketApp.Web.Layout.MainLayout.LightenColor("#003399");
        Assert.StartsWith("#", result);
        Assert.Equal(7, result.Length);
        // #003399 → R=0+40=40(28), G=51+40=91(5B), B=153+40=193(C1)
        Assert.Equal("#285BC1", result);
    }

    [Fact]
    public void LightenColor_nearWhite_clampsto255()
    {
        var result = THWTicketApp.Web.Layout.MainLayout.LightenColor("#F0F0F0");
        Assert.Equal("#FFFFFF", result);
    }

    [Fact]
    public void LightenColor_shortHex_returnsAsIs()
    {
        Assert.Equal("#FFF", THWTicketApp.Web.Layout.MainLayout.LightenColor("#FFF"));
    }
}
