using THWTicketApp.Web.Components;

namespace THWTicketApp.Tests.Components;

/// <summary>
/// Tests for the deterministic helpers inside UserAvatar — the rendering
/// itself is trivial Mud markup and not worth bUnit overhead.
/// </summary>
public class UserAvatarTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Hans Müller", "HM")]
    [InlineData("hans müller", "HM")]
    [InlineData("trudesk", "T")]
    [InlineData("Anna-Maria Schmitz", "AS")] // Hyphen part of first token, picks A + S
    [InlineData("Three Word Name", "TW")]    // Only first two parts used
    public void InitialsFor_returnsExpected(string input, string expected)
    {
        Assert.Equal(expected, UserAvatar.InitialsFor(input));
    }

    [Fact]
    public void ColorFor_isDeterministicAcrossCalls()
    {
        // Same input → same output, even on repeated invocations.
        Assert.Equal(UserAvatar.ColorFor("Hans"), UserAvatar.ColorFor("Hans"));
    }

    [Fact]
    public void ColorFor_differsForDifferentNames()
    {
        // Not a strict guarantee (hash collisions exist), but spot-check
        // that we're not always returning the same color.
        var a = UserAvatar.ColorFor("Anna Schmitz");
        var b = UserAvatar.ColorFor("Bernd Müller");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ColorFor_emptyName_returnsFallbackGray()
    {
        Assert.Equal("#9E9E9E", UserAvatar.ColorFor(""));
    }

    [Fact]
    public void ColorFor_returnsHslFormat()
    {
        var color = UserAvatar.ColorFor("Test");
        Assert.StartsWith("hsl(", color);
        Assert.EndsWith(")", color);
        Assert.Contains("55%", color);
        Assert.Contains("45%", color);
    }
}
