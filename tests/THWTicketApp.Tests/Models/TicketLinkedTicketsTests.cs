using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Models;

/// <summary>
/// Tickets gained a `linkedTickets` array (trudesk v2 bidirectional links).
/// The server populates each entry's nested ticket with uid/subject/status and
/// a linkType. Old tickets don't carry the field — the model must default to an
/// empty list so the panel can iterate without null checks.
/// </summary>
public class TicketLinkedTicketsTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_populatedArray_mapsNestedTicketAndType()
    {
        const string json = """
            {
                "_id": "t1",
                "uid": 1001,
                "linkedTickets": [
                    {
                        "ticket": {
                            "_id": "t2",
                            "uid": 1002,
                            "subject": "Verwandtes Ticket",
                            "status": { "name": "Offen", "htmlColor": "#29b955", "isResolved": false }
                        },
                        "linkType": "related"
                    },
                    {
                        "ticket": { "_id": "t3", "uid": 1003, "subject": "Blockierer" },
                        "linkType": "blockedBy"
                    }
                ]
            }
            """;

        var ticket = JsonSerializer.Deserialize<Ticket>(json, Options)!;

        Assert.Equal(2, ticket.LinkedTickets.Count);
        Assert.Equal(1002, ticket.LinkedTickets[0].Ticket!.Uid);
        Assert.Equal("Verwandtes Ticket", ticket.LinkedTickets[0].Ticket!.Subject);
        Assert.Equal("Offen", ticket.LinkedTickets[0].Ticket!.Status!.Name);
        Assert.Equal("#29b955", ticket.LinkedTickets[0].Ticket!.Status!.HtmlColor);
        Assert.Equal("related", ticket.LinkedTickets[0].LinkType);
        Assert.Equal("blockedBy", ticket.LinkedTickets[1].LinkType);
        Assert.Equal(1003, ticket.LinkedTickets[1].Ticket!.Uid);
    }

    [Fact]
    public void Deserialize_missingField_defaultsToEmptyList()
    {
        var ticket = JsonSerializer.Deserialize<Ticket>("{\"_id\":\"t1\",\"uid\":1}", Options)!;

        Assert.NotNull(ticket.LinkedTickets);
        Assert.Empty(ticket.LinkedTickets);
    }
}
