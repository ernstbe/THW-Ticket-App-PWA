using System.Net;
using System.Text.RegularExpressions;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Strip enough HTML from a trudesk-stored Issue/comment body to drop it
/// into a plain <c>MudTextField</c> for editing without the user seeing
/// raw <c>&lt;p&gt;</c> tags. trudesk's web UI ships with a rich-text
/// editor that wraps even one-line descriptions in <c>&lt;p&gt;…&lt;/p&gt;</c>.
///
/// Empty lines the user typed must survive the edit round-trip. trudesk
/// stores the body in two shapes: ticket CREATE turns each newline into a
/// <c>&lt;br&gt;</c> (so blank lines are <c>&lt;br&gt;</c> runs), while ticket
/// UPDATE stores the raw text with literal newlines. Both kinds of break are
/// protected from the blank-line collapse below; only the structural padding
/// that successive <c>&lt;/p&gt;</c> tags (and cosmetic newlines between block
/// tags) introduce is collapsed.
/// </summary>
public static class HtmlToPlainText
{
    private static readonly Regex BrTagRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParagraphCloseRegex = new(@"</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListItemOpenRegex = new(@"<li[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListItemCloseRegex = new(@"</li\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex CollapseBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    // Placeholder that shields explicit <br> breaks while the structural
    // blank-line collapse runs, so multiple <br>'s are never merged. (Raw
    // newlines in tagless bodies are shielded by skipping the collapse
    // entirely.) U+0001 cannot occur in sanitized ticket text.
    private const char BreakPlaceholder = '\u0001';

    public static string Convert(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // A body with no tags is the raw-newline shape (trudesk UPDATE): its
        // newlines are the user's own and must be kept verbatim, so only the
        // tag-derived padding collapse is skipped for it.
        var hasTags = AnyTagRegex.IsMatch(html);

        // Protect explicit <br> breaks, then map structural tags to whitespace
        // before stripping the rest so paragraphs and list items survive.
        var s = BrTagRegex.Replace(html, BreakPlaceholder.ToString());
        s = ParagraphCloseRegex.Replace(s, "\n\n");
        s = ListItemOpenRegex.Replace(s, "- ");
        s = ListItemCloseRegex.Replace(s, "\n");

        // Strip every remaining tag.
        s = AnyTagRegex.Replace(s, string.Empty);

        // Decode entities (&amp; &nbsp; &#39; etc.).
        s = WebUtility.HtmlDecode(s);

        // Collapse runs of >2 blank lines that successive </p>'s (or cosmetic
        // newlines between block tags) introduce — but only for HTML bodies,
        // and never across the protected user breaks.
        if (hasTags)
        {
            s = CollapseBlankLines.Replace(s, "\n\n");
        }

        // Restore the protected user breaks as real newlines.
        s = s.Replace(BreakPlaceholder, '\n');

        return s.Trim();
    }
}
