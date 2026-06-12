using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// Consolidated parser coverage — these cases used to live twice, once in
/// AddTicketTests (against AddTicket.ParseTemplates) and once in
/// TemplatesTests (against Templates.ParseTemplates). The shared parser
/// keeps the stricter Templates semantics: ref names are extracted (raw,
/// untranslated), blank checklist titles are skipped.
/// </summary>
public class TicketTemplateParserTests
{
    [Fact]
    public void ParseTemplates_emptyObject_returnsEmptyList()
    {
        Assert.Empty(TicketTemplateParser.ParseTemplates("{}"));
    }

    [Fact]
    public void ParseTemplates_missingKey_returnsEmpty()
    {
        Assert.Empty(TicketTemplateParser.ParseTemplates("""{"other":[]}"""));
    }

    [Fact]
    public void ParseTemplates_emptyArray_returnsEmpty()
    {
        Assert.Empty(TicketTemplateParser.ParseTemplates("""{"ticketTemplates":[]}"""));
    }

    [Fact]
    public void ParseTemplates_invalidJson_returnsEmpty()
    {
        Assert.Empty(TicketTemplateParser.ParseTemplates("not json"));
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

        var result = TicketTemplateParser.ParseTemplates(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Einsatznachbereitung", result[0].Name);
        Assert.Equal("Einsatz vom {date}", result[0].Subject);
        Assert.Equal("Vollständiger Bericht", result[0].Issue);
        Assert.Equal("Fahrzeugausfall", result[1].Name);
        Assert.Null(result[1].Issue);
    }

    [Fact]
    public void ParseTemplates_populatedTypeAndPriority_extractsIdAndRawName()
    {
        // Names come back raw — translation (e.g. "High" → "Hoch") is
        // presentation logic and stays in the pages.
        const string json = """
        {
            "ticketTemplates": [{
                "_id":"1","name":"T","subject":"S",
                "ticketType":{"_id":"t1","name":"Vorfall"},
                "priority":{"_id":"p1","name":"High"}
            }]
        }
        """;
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Equal("t1", result[0].TypeId);
        Assert.Equal("Vorfall", result[0].TypeName);
        Assert.Equal("p1", result[0].PriorityId);
        Assert.Equal("High", result[0].PriorityName);
    }

    [Fact]
    public void ParseTemplates_refAsBareString_stillExtractsId()
    {
        const string json = """
        {"ticketTemplates":[{"_id":"1","name":"T","subject":"S","ticketType":"t1","priority":"p1"}]}
        """;
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Equal("t1", result[0].TypeId);
        Assert.Null(result[0].TypeName);
        Assert.Equal("p1", result[0].PriorityId);
    }

    [Fact]
    public void ParseTemplates_missingFields_returnsEmptyStrings()
    {
        const string json = "{\"ticketTemplates\":[{\"_id\":\"x\"}]}";
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Single(result);
        Assert.Equal("x", result[0].Id);
        Assert.Equal("", result[0].Name);
        Assert.Equal("", result[0].Subject);
        Assert.Null(result[0].Issue);
        Assert.Null(result[0].TypeId);
        Assert.Null(result[0].PriorityId);
    }

    [Fact]
    public void ParseTemplates_checklist_extractsTitlesInOrder()
    {
        const string json = """
        {
            "ticketTemplates": [{
                "_id":"1","name":"T","subject":"S",
                "checklist":[
                    {"_id":"c1","title":"Fahrzeug prüfen"},
                    {"_id":"c2","title":"Material zählen"}
                ]
            }]
        }
        """;
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Equal(new[] { "Fahrzeug prüfen", "Material zählen" }, result[0].Checklist);
    }

    [Fact]
    public void ParseTemplates_missingChecklist_returnsEmptyList()
    {
        // Templates created before the checklist feature have no key at all.
        const string json = """{"ticketTemplates":[{"_id":"1","name":"T","subject":"S"}]}""";
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Empty(result[0].Checklist);
    }

    [Fact]
    public void ParseTemplates_checklistWithBlankTitles_skipsThem()
    {
        const string json = """
        {"ticketTemplates":[{"_id":"1","name":"T","subject":"S","checklist":[{"title":""},{"title":"OK"},{"_id":"x"}]}]}
        """;
        var result = TicketTemplateParser.ParseTemplates(json);
        Assert.Equal(new[] { "OK" }, result[0].Checklist);
    }

    [Fact]
    public void ParseTemplates_multipleTemplates_parsesAll()
    {
        const string json = """{"ticketTemplates":[{"name":"A","subject":"S1"},{"name":"B","subject":"S2","issue":"I2"}]}""";
        var result = TicketTemplateParser.ParseTemplates(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Null(result[0].Issue);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("I2", result[1].Issue);
    }
}
