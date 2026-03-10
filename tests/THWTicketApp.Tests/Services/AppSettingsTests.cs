using THWTicketApp.Shared.Services;

namespace THWTicketApp.Tests.Services;

public class AppSettingsTests
{
    [Fact]
    public void IsConfigured_DefaultEmpty_ReturnsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WithUrl_ReturnsTrue()
    {
        var settings = new AppSettings { ApiBaseUrl = "https://example.com/api/v2" };
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void IsConfigured_Whitespace_ReturnsFalse()
    {
        var settings = new AppSettings { ApiBaseUrl = "   " };
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void DefaultTimeout_Is30Seconds()
    {
        var settings = new AppSettings();
        Assert.Equal(30, settings.ConnectionTimeoutSeconds);
    }
}
