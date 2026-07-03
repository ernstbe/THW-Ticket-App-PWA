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

    // Recognizes the HTML (trudesk CREATE / rich-editor) shape by an actual
    // known HTML tag — NOT any "<...>" token. A raw-text UPDATE body that merely
    // contains an identifier like <VLAN>, <Name> or <Server01> must not be
    // mistaken for markup and stripped. Tag name is anchored with \b so <codex>
    // does not match <code>, etc.
    private static readonly Regex HtmlTagRegex = new(
        @"</?(?:p|br|div|span|ul|ol|li|a|strong|b|em|i|u|s|strike|del|ins|h[1-6]|blockquote|pre|code|table|thead|tbody|tr|td|th|hr|img)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Placeholder that shields explicit <br> breaks while the structural
    // blank-line collapse runs, so multiple <br>'s are never merged. (Raw
    // newlines in tagless bodies are shielded by skipping the collapse
    // entirely.) U+0001 cannot occur in sanitized ticket text.
    private const char BreakPlaceholder = '\u0001';

    public static string Convert(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // The raw-newline shape (trudesk UPDATE) carries no HTML tags: keep the
        // user's text — including any <VLAN>-style token and their blank lines —
        // verbatim, only decoding entities. Running the tag transforms below on
        // it would delete such tokens and collapse blank lines (data loss).
        if (!HtmlTagRegex.IsMatch(html))
        {
            return WebUtility.HtmlDecode(html).Trim();
        }

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
        // newlines between block tags) introduce — never across the protected
        // user breaks, which are restored afterwards.
        s = CollapseBlankLines.Replace(s, "\n\n");

        // Restore the protected user breaks as real newlines.
        s = s.Replace(BreakPlaceholder, '\n');

        return s.Trim();
    }
}
