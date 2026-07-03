using THWTicketApp.Shared.Services;

namespace THWTicketApp.Tests.Services;

public class NotificationTitleTranslationTests
{
    [Theory]
    // frontend-review ISSUE-5: English server titles → German, "Ticket#" spacing normalized.
    [InlineData("Ticket #1098 Created", "Ticket #1098 erstellt")]
    [InlineData("Comment Added to Ticket#1098", "Neuer Kommentar zu Ticket #1098")]
    [InlineData("You were mentioned in Ticket#42", "Du wurdest in Ticket #42 erwähnt")]
    public void TranslatesEnglishTitlesToGerman(string input, string expected)
        => Assert.Equal(expected, TrudeskTranslationHelper.TranslateNotificationTitle(input));

    [Theory]
    // Already German (assignment) or unknown titles pass through unchanged.
    [InlineData("Ticket #77 dir zugewiesen")]
    [InlineData("Irgendein anderer Titel")]
    public void LeavesGermanOrUnknownTitlesUnchanged(string input)
        => Assert.Equal(input, TrudeskTranslationHelper.TranslateNotificationTitle(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandlesNullOrEmpty(string? input)
        => Assert.Equal(input, TrudeskTranslationHelper.TranslateNotificationTitle(input));
}
