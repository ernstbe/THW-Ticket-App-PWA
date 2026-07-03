using THWTicketApp.Shared.Services;

namespace THWTicketApp.Tests.Services;

public class HistoryPriorityTranslationTests
{
    // #222: priority values in history rows used to stay English.
    [Theory]
    [InlineData("High", "Hoch")]
    [InlineData("Critical", "Kritisch")]
    [InlineData("Urgent", "Dringend")]
    [InlineData("Low", "Niedrig")]
    public void TranslateStatusOrPriority_translatesPriorities(string input, string expected)
        => Assert.Equal(expected, TrudeskTranslationHelper.TranslateStatusOrPriority(input));

    [Theory]
    [InlineData("New", "Neu")]
    [InlineData("Closed", "Geschlossen")]
    public void TranslateStatusOrPriority_stillTranslatesStatuses(string input, string expected)
        => Assert.Equal(expected, TrudeskTranslationHelper.TranslateStatusOrPriority(input));

    [Fact]
    public void HistoryDescription_priorityValue_isGerman()
    {
        // "Ticket Priority set to: High" → just the translated value.
        Assert.Equal("Hoch", TrudeskTranslationHelper.TranslateHistoryDescription("Ticket Priority set to: High"));
    }

    [Fact]
    public void HistoryAction_dynamicPrioritySuffix_isGerman()
    {
        // "ticket:set:priority:High" → "Priorität geändert: Hoch".
        Assert.Equal("Priorität geändert: Hoch", TrudeskTranslationHelper.TranslateHistoryAction("ticket:set:priority:High"));
    }
}
