using System.Text.RegularExpressions;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Markdown treats any run of one or more blank lines as a single paragraph
/// break, so a description the user typed with several empty lines loses that
/// spacing once it is rendered. trudesk's ticket UPDATE endpoint stores the
/// body as plain text with raw newlines (CREATE wraps them in <c>&lt;br&gt;</c>),
/// so the first edit-and-save round-trip turns multi-line descriptions into a
/// Markdig render where the extra empty lines simply vanish.
///
/// Replacing each whitespace-only line with a single non-breaking space keeps
/// every blank line visible: combined with the renderer's
/// <c>UseSoftlineBreakAsHardlineBreak</c> option each preserved line becomes a
/// hard break, while inline markup and bullet lists keep working.
/// </summary>
public static class MarkdownBlankLines
{
    // Multiline: match every line that is empty or contains only spaces/tabs,
    // consuming an optional CR so \r\n endings are handled too.
    private static readonly Regex BlankLineRegex =
        new(@"(?m)^[ \t]*\r?$", RegexOptions.Compiled);

    // U+00A0 (non-breaking space): a line of ordinary spaces still counts as
    // blank to Markdig, so the kept line needs real, non-collapsible content
    // to survive as a hard break.
    private const string NonBreakingSpace = "\u00A0";

    public static string Preserve(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return BlankLineRegex.Replace(text, NonBreakingSpace);
    }
}
