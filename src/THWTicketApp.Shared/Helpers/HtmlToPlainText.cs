using System.Net;
using System.Text.RegularExpressions;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Strip enough HTML from a trudesk-stored Issue/comment body to drop it
/// into a plain <c>MudTextField</c> for editing without the user seeing
/// raw <c>&lt;p&gt;</c> tags. trudesk's web UI ships with a rich-text
/// editor that wraps even one-line descriptions in <c>&lt;p&gt;…&lt;/p&gt;</c>.
/// </summary>
public static class HtmlToPlainText
{
    private static readonly Regex BrTagRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParagraphCloseRegex = new(@"</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListItemOpenRegex = new(@"<li[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListItemCloseRegex = new(@"</li\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex CollapseBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    public static string Convert(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Map structural tags to whitespace before stripping the rest, so
        // paragraphs and list items survive as line breaks.
        var s = BrTagRegex.Replace(html, "\n");
        s = ParagraphCloseRegex.Replace(s, "\n\n");
        s = ListItemOpenRegex.Replace(s, "- ");
        s = ListItemCloseRegex.Replace(s, "\n");

        // Strip every remaining tag.
        s = AnyTagRegex.Replace(s, string.Empty);

        // Decode entities (&amp; &nbsp; &#39; etc.).
        s = WebUtility.HtmlDecode(s);

        // Trim & collapse runs of >2 blank lines from successive </p>'s.
        s = CollapseBlankLines.Replace(s, "\n\n").Trim();

        return s;
    }
}
