using Markdig;
using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// Lukas reported that empty lines a user types into a ticket description
/// vanish after saving: trudesk's UPDATE stores raw newlines and Markdig
/// collapses runs of blank lines into a single paragraph break. Pin the
/// preservation helper plus its interaction with the render pipeline.
/// </summary>
public class MarkdownBlankLinesTests
{
    private const string Nbsp = "\u00A0";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("no blanks", "no blanks")]
    public void Preserve_passes_through_text_without_blank_lines(string? input, string expected)
    {
        Assert.Equal(expected, MarkdownBlankLines.Preserve(input));
    }

    [Fact]
    public void Preserve_replaces_each_blank_line_with_a_non_breaking_space()
    {
        Assert.Equal($"A\n{Nbsp}\n{Nbsp}\nB", MarkdownBlankLines.Preserve("A\n\n\nB"));
    }

    [Fact]
    public void Preserve_treats_whitespace_only_lines_as_blank()
    {
        Assert.Equal($"A\n{Nbsp}\nB", MarkdownBlankLines.Preserve("A\n  \t \nB"));
    }

    [Fact]
    public void Preserve_handles_crlf_line_endings()
    {
        // The blank line uses a CRLF ending; the helper must still recognise
        // and fill it (Markdig normalises the surviving line endings later).
        Assert.Contains(Nbsp, MarkdownBlankLines.Preserve("A\r\n\r\nB"));
    }

    [Fact]
    public void Preserve_leaves_bullet_lists_intact()
    {
        const string list = "- a\n- b";
        Assert.Equal(list, MarkdownBlankLines.Preserve(list));
    }

    [Fact]
    public void Rendering_preserved_text_keeps_multiple_blank_lines()
    {
        // Two blank lines between the words must survive as hard breaks rather
        // than collapsing into a single paragraph gap.
        var raw = "Erste Zeile\n\n\nLetzte Zeile";

        var collapsed = Markdown.ToHtml(raw, Pipeline);
        var preserved = Markdown.ToHtml(MarkdownBlankLines.Preserve(raw), Pipeline);

        // Without the helper Markdig collapses the empty lines into a single
        // paragraph break, so no hard breaks survive (the reported bug).
        Assert.True(
            CountOccurrences(collapsed, "<br") < 3,
            $"expected the blank lines to collapse without the helper, got: {collapsed}");
        // With it the filled lines are no longer blank at block-parse time, so
        // every newline survives as a hard break and the spacing is kept.
        Assert.True(
            CountOccurrences(preserved, "<br") >= 3,
            $"expected three hard breaks to preserve two blank lines, got: {preserved}");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
