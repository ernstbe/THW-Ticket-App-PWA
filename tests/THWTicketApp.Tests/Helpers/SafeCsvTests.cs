using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class SafeCsvTests
{
    [Theory]
    // #257: leading formula triggers must be neutralized with a leading quote.
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1+1", "\"'+1+1\"")]
    [InlineData("-2", "\"'-2\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    public void Field_neutralizesFormulaTriggers(string input, string expected)
        => Assert.Equal(expected, SafeCsv.Field(input));

    [Theory]
    [InlineData("Pumpe defekt", "\"Pumpe defekt\"")]
    [InlineData("", "\"\"")]
    [InlineData(null, "\"\"")]
    public void Field_leavesNormalTextQuotedButUnprefixed(string? input, string expected)
        => Assert.Equal(expected, SafeCsv.Field(input));

    [Fact]
    public void Field_doublesEmbeddedQuotes()
        => Assert.Equal("\"a\"\"b\"", SafeCsv.Field("a\"b"));

    [Fact]
    public void Field_quotesEmbeddedDelimiterAndNewline()
        => Assert.Equal("\"a;b\nc\"", SafeCsv.Field("a;b\nc"));
}
