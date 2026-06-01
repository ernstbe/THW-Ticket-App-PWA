using Markdig;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// Pin Markdig pipeline behaviour the MarkdownToolbar relies on, so a
/// future Markdig upgrade or extension toggle can't silently break the
/// toolbar buttons (Lukas reported ++Underline++ rendering as literal
/// text after the toolbar shipped).
/// </summary>
public class MarkdownPipelineTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    [Theory]
    [InlineData("**bold**", "<strong>bold</strong>")]
    [InlineData("*italic*", "<em>italic</em>")]
    [InlineData("~~strike~~", "<del>strike</del>")]
    [InlineData("`code`", "<code>code</code>")]
    [InlineData("++underline++", "<ins>underline</ins>")]
    public void Pipeline_renders_toolbar_markup(string input, string expectedFragment)
    {
        var html = Markdown.ToHtml(input, Pipeline);
        Assert.Contains(expectedFragment, html);
    }
}
