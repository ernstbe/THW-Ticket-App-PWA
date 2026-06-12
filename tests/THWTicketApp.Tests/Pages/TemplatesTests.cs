using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class TemplatesTests
{
    // Template parsing moved to the shared TicketTemplateParser —
    // see Helpers/TicketTemplateParserTests. The page only adds the
    // priority-name translation on top.

    // -----------------------------------------------------------------
    // TranslatePriorityNames
    // -----------------------------------------------------------------

    [Fact]
    public void TranslatePriorityNames_translatesKnownNames_keepsNulls()
    {
        var templates = new List<TicketTemplate>
        {
            new() { PriorityName = "High" },
            new() { PriorityName = null },
            new() { PriorityName = "Sondermeldung" },
        };

        Templates.TranslatePriorityNames(templates);

        Assert.Equal("Hoch", templates[0].PriorityName);
        Assert.Null(templates[1].PriorityName);
        // Unknown names pass through untouched.
        Assert.Equal("Sondermeldung", templates[2].PriorityName);
    }

    // -----------------------------------------------------------------
    // ValidateForm
    // -----------------------------------------------------------------

    [Fact]
    public void ValidateForm_bothFieldsPresent_returnsNull()
    {
        Assert.Null(Templates.ValidateForm("Name", "Subject"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateForm_missingName_returnsNameError(string? name)
    {
        Assert.Equal("templates.name_required", Templates.ValidateForm(name, "Subject"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateForm_missingSubject_returnsSubjectError(string? subject)
    {
        Assert.Equal("templates.subject_required", Templates.ValidateForm("Name", subject));
    }

    // -----------------------------------------------------------------
    // BuildPayload
    // -----------------------------------------------------------------

    [Fact]
    public void BuildPayload_trimsAllFields()
    {
        var payload = Templates.BuildPayload("  Name  ", "  Subject  ", "  Body  ");
        Assert.Equal("Name", payload["name"]);
        Assert.Equal("Subject", payload["subject"]);
        Assert.Equal("Body", payload["issue"]);
    }

    [Fact]
    public void BuildPayload_whitespaceIssueBecomesNull()
    {
        var payload = Templates.BuildPayload("Name", "Subject", "   ");
        Assert.Null(payload["issue"]);
    }

    [Fact]
    public void BuildPayload_nullIssueStaysNull()
    {
        var payload = Templates.BuildPayload("Name", "Subject", null);
        Assert.Null(payload["issue"]);
    }

    [Fact]
    public void BuildPayload_typeAndPriority_includedWhenSet()
    {
        var payload = Templates.BuildPayload("N", "S", null, "type-1", "prio-1");
        Assert.Equal("type-1", payload["ticketType"]);
        Assert.Equal("prio-1", payload["priority"]);
    }

    [Fact]
    public void BuildPayload_typeAndPriority_omittedWhenBlank()
    {
        var payload = Templates.BuildPayload("N", "S", null, "", "  ");
        Assert.False(payload.ContainsKey("ticketType"));
        Assert.False(payload.ContainsKey("priority"));
    }

    [Fact]
    public void BuildPayload_checklist_wrapsTitlesAsObjects()
    {
        var payload = Templates.BuildPayload("N", "S", null,
            checklist: new List<string> { "Eins", "Zwei" });

        var items = Assert.IsType<List<Dictionary<string, object?>>>(payload["checklist"]);
        Assert.Equal(2, items.Count);
        Assert.Equal("Eins", items[0]["title"]);
        Assert.Equal("Zwei", items[1]["title"]);
    }

    [Fact]
    public void BuildPayload_noChecklist_sendsEmptyArray()
    {
        // The key must always be present: PUT replaces the whole array,
        // so omitting it would make "delete all items" not stick.
        var payload = Templates.BuildPayload("N", "S", null);
        var items = Assert.IsType<List<Dictionary<string, object?>>>(payload["checklist"]);
        Assert.Empty(items);
    }
}
