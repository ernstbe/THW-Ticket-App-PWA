using Ganss.Xss;

namespace THWTicketApp.Web.Helpers;

/// <summary>
/// Sanitizes HTML before it is emitted via (MarkupString).
///
/// Trudesk stores comment/note bodies (and builds reports) as HTML — real
/// markup like &lt;p&gt;, &lt;br&gt; and &lt;a href="mailto:…"&gt; that must
/// keep rendering. We therefore cannot simply escape all HTML (that shows the
/// tags as literal text). Instead we allow a safe subset and strip anything
/// dangerous — script/style, event handlers (onerror/onload), javascript:
/// URLs, etc. — which closes the stored-XSS hole (issues #191/#195/#198)
/// without breaking legitimate formatting.
/// </summary>
public static class SafeHtml
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        // HtmlSanitizer's defaults already allow the common formatting tags
        // (p, br, a, strong, em, ul/ol/li, blockquote, code/pre, tables, …)
        // and drop script/style, event-handler attributes and unsafe URL
        // schemes. We only need to permit the link schemes our content uses.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");
        return sanitizer;
    }

    /// <summary>Return <paramref name="html"/> with unsafe markup removed.</summary>
    public static string Sanitize(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : Sanitizer.Sanitize(html);
}
