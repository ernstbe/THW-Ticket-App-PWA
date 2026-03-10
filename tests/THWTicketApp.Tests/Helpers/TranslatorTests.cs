using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class TranslatorTests
{
    [Theory]
    [InlineData("Open", "Offen")]
    [InlineData("Closed", "Geschlossen")]
    [InlineData("High", "Hoch")]
    [InlineData("Low", "Niedrig")]
    [InlineData("Critical", "Kritisch")]
    [InlineData("In Progress", "In Bearbeitung")]
    [InlineData("New", "Neu")]
    [InlineData("Resolved", "Gelöst")]
    public void Translate_KnownTerms_ReturnsGerman(string input, string expected)
    {
        Assert.Equal(expected, Translator.Translate(input));
    }

    [Theory]
    [InlineData("open", "Offen")]
    [InlineData("CLOSED", "Geschlossen")]
    [InlineData("high", "Hoch")]
    public void Translate_CaseInsensitive(string input, string expected)
    {
        Assert.Equal(expected, Translator.Translate(input));
    }

    [Fact]
    public void Translate_UnknownTerm_ReturnsOriginal()
    {
        Assert.Equal("SomethingUnknown", Translator.Translate("SomethingUnknown"));
    }

    [Fact]
    public void Translate_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Translator.Translate(null));
    }

    [Fact]
    public void Translate_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Translator.Translate(string.Empty));
    }
}
