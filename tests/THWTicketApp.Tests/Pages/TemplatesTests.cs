using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class TemplatesTests
{
    // -----------------------------------------------------------------
    // ParseTemplates
    // -----------------------------------------------------------------

    [Fact]
    public void ParseTemplates_emptyObject_returnsEmptyList()
    {
        var result = Templates.ParseTemplates("{}");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_malformed_returnsEmptyList()
    {
        var result = Templates.ParseTemplates("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_validWrappedResponse_returnsEntries()
    {
        const string json = """
        {
            "ticketTemplates": [
                {"_id":"1","name":"Einsatznachbereitung","subject":"Einsatz vom {date}","issue":"Vollständiger Bericht"},
                {"_id":"2","name":"Fahrzeugausfall","subject":"Ausfall {vehicle}","issue":null}
            ]
        }
        """;

        var result = Templates.ParseTemplates(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Einsatznachbereitung", result[0].Name);
        Assert.Equal("Einsatz vom {date}", result[0].Subject);
        Assert.Equal("Vollständiger Bericht", result[0].Issue);
        Assert.Null(result[1].Issue);
    }

    [Fact]
    public void ParseTemplates_populatedTypeAndPriority_extractsIdAndName()
    {
        const string json = """
        {
            "ticketTemplates": [{
                "_id":"1","name":"T","subject":"S",
                "ticketType":{"_id":"t1","name":"Vorfall"},
                "priority":{"_id":"p1","name":"High"}
            }]
        }
        """;
        var result = Templates.ParseTemplates(json);
        Assert.Equal("t1", result[0].TypeId);
        Assert.Equal("Vorfall", result[0].TypeName);
        Assert.Equal("p1", result[0].PriorityId);
        Assert.Equal("Hoch", result[0].PriorityName);
    }

    [Fact]
    public void ParseTemplates_refAsBareString_stillExtractsId()
    {
        const string json = """
        {"ticketTemplates":[{"_id":"1","name":"T","subject":"S","ticketType":"t1","priority":"p1"}]}
        """;
        var result = Templates.ParseTemplates(json);
        Assert.Equal("t1", result[0].TypeId);
        Assert.Null(result[0].TypeName);
        Assert.Equal("p1", result[0].PriorityId);
    }

    [Fact]
    public void ParseTemplates_missingFields_returnsEmptyStrings()
    {
        const string json = "{\"ticketTemplates\":[{\"_id\":\"x\"}]}";
        var result = Templates.ParseTemplates(json);
        Assert.Single(result);
        Assert.Equal("x", result[0].Id);
        Assert.Equal("", result[0].Name);
        Assert.Equal("", result[0].Subject);
        Assert.Null(result[0].Issue);
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
}
