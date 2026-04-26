using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class AddTicketTests
{
    [Fact]
    public void ParseTemplates_validJson_returnsList()
    {
        var json = """{"ticketTemplates":[{"_id":"1","name":"Einsatz","subject":"Einsatz vom {date}","issue":"Bericht..."}]}""";
        var result = AddTicket.ParseTemplates(json);

        Assert.Single(result);
        Assert.Equal("Einsatz", result[0].Name);
        Assert.Equal("Einsatz vom {date}", result[0].Subject);
        Assert.Equal("Bericht...", result[0].Issue);
    }

    [Fact]
    public void ParseTemplates_emptyArray_returnsEmpty()
    {
        var json = """{"ticketTemplates":[]}""";
        var result = AddTicket.ParseTemplates(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_missingKey_returnsEmpty()
    {
        var json = """{"other":[]}""";
        var result = AddTicket.ParseTemplates(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_invalidJson_returnsEmpty()
    {
        var result = AddTicket.ParseTemplates("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemplates_multipleTemplates_parsesAll()
    {
        var json = """{"ticketTemplates":[{"name":"A","subject":"S1"},{"name":"B","subject":"S2","issue":"I2"}]}""";
        var result = AddTicket.ParseTemplates(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Null(result[0].Issue);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("I2", result[1].Issue);
    }
}
