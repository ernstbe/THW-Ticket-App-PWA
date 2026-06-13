using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class HtmlToPlainTextTests
{
    [Fact]
    public void Convert_null_returnsEmpty()
        => Assert.Equal(string.Empty, HtmlToPlainText.Convert(null));

    [Fact]
    public void Convert_empty_returnsEmpty()
        => Assert.Equal(string.Empty, HtmlToPlainText.Convert(string.Empty));

    [Fact]
    public void Convert_plainText_returnedUnchanged()
        => Assert.Equal("Hello world", HtmlToPlainText.Convert("Hello world"));

    [Fact]
    public void Convert_singleParagraph_stripsWrapper()
    {
        // The shape trudesk actually returned for ticket 1014 in production.
        var input = "<p>Helfer benötigt eine neue Schuhgröße in 39</p>";
        Assert.Equal("Helfer benötigt eine neue Schuhgröße in 39", HtmlToPlainText.Convert(input));
    }

    [Fact]
    public void Convert_brTag_becomesNewline()
        => Assert.Equal("line one\nline two", HtmlToPlainText.Convert("line one<br>line two"));

    [Fact]
    public void Convert_selfClosingBr_becomesNewline()
        => Assert.Equal("line one\nline two", HtmlToPlainText.Convert("line one<br />line two"));

    [Fact]
    public void Convert_twoParagraphs_separatedByBlankLine()
    {
        var input = "<p>first</p><p>second</p>";
        Assert.Equal("first\n\nsecond", HtmlToPlainText.Convert(input));
    }

    [Fact]
    public void Convert_listItems_becomeDashedLines()
    {
        var input = "<ul><li>one</li><li>two</li></ul>";
        var expected = "- one\n- two";
        Assert.Equal(expected, HtmlToPlainText.Convert(input));
    }

    [Fact]
    public void Convert_htmlEntities_areDecoded()
    {
        Assert.Equal("a & b", HtmlToPlainText.Convert("a &amp; b"));
        Assert.Equal("a < b", HtmlToPlainText.Convert("a &lt; b"));
        Assert.Equal("a > b", HtmlToPlainText.Convert("a &gt; b"));
        // WebUtility.HtmlDecode emits U+00A0 (non-breaking space) for &nbsp;.
        Assert.Equal("a   b", HtmlToPlainText.Convert("a &nbsp; b"));
    }

    [Fact]
    public void Convert_stripsArbitraryTags()
    {
        var input = "<strong>bold</strong> and <em>italic</em>";
        Assert.Equal("bold and italic", HtmlToPlainText.Convert(input));
    }

    [Fact]
    public void Convert_collapsesExcessBlankLines()
    {
        // Two empty paragraphs in between should not blow the gap out to
        // four blank lines.
        var input = "<p>a</p><p></p><p></p><p>b</p>";
        var result = HtmlToPlainText.Convert(input);
        Assert.Equal("a\n\nb", result);
    }

    [Fact]
    public void Convert_rawNewlines_arePreservedVerbatim()
    {
        // trudesk UPDATE stores the body as raw text with literal newlines;
        // the empty lines the user typed must survive re-opening the editor.
        var input = "Zeile A\n\n\nZeile B";
        Assert.Equal("Zeile A\n\n\nZeile B", HtmlToPlainText.Convert(input));
    }

    [Fact]
    public void Convert_multipleBrTags_arePreserved()
    {
        // trudesk ticket CREATE turns each newline into a <br>, so a blank
        // line is two <br>'s — they must not collapse into a single break.
        Assert.Equal("a\n\nb", HtmlToPlainText.Convert("a<br><br>b"));
    }

    [Fact]
    public void Convert_paragraphWithBrBlankLines_keepsEveryLine()
    {
        // The exact CREATE shape: a single <p> wrapper around <br>-joined
        // lines, including two blank lines the user inserted.
        var input = "<p>A<br><br><br>B</p>";
        Assert.Equal("A\n\n\nB", HtmlToPlainText.Convert(input));
    }
}
