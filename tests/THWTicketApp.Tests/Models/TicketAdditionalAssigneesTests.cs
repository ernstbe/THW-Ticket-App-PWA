using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Models;

/// <summary>
/// Tickets gained an `additionalAssignees` array (populated user objects,
/// same shape as `assignee`) with the multi-assignee feature. Old tickets
/// don't carry the field at all — the model must default to an empty list
/// so filters can use it without null checks.
/// </summary>
public class TicketAdditionalAssigneesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_populatedArray_mapsUserObjects()
    {
        const string json = """
            {
                "_id": "t1",
                "uid": 1001,
                "assignee": { "_id": "u-primary", "username": "chef", "fullname": "Haupt Verantwortlicher" },
                "additionalAssignees": [
                    { "_id": "u1", "username": "anna", "fullname": "Anna Alpha", "email": "a@thw.test" },
                    { "_id": "u2", "username": "bernd", "fullname": "Bernd Beta" }
                ]
            }
            """;

        var ticket = JsonSerializer.Deserialize<Ticket>(json, Options)!;

        Assert.Equal(2, ticket.AdditionalAssignees.Count);
        Assert.Equal("u1", ticket.AdditionalAssignees[0].Id);
        Assert.Equal("Anna Alpha", ticket.AdditionalAssignees[0].Fullname);
        Assert.Equal("u2", ticket.AdditionalAssignees[1].Id);
        Assert.Equal("u-primary", ticket.Assignee!.Id);
    }

    [Fact]
    public void Deserialize_missingField_defaultsToEmptyList()
    {
        var ticket = JsonSerializer.Deserialize<Ticket>("{\"_id\":\"t1\",\"uid\":1}", Options)!;

        Assert.NotNull(ticket.AdditionalAssignees);
        Assert.Empty(ticket.AdditionalAssignees);
    }

    [Fact]
    public void Deserialize_emptyArray_yieldsEmptyList()
    {
        var ticket = JsonSerializer.Deserialize<Ticket>(
            "{\"_id\":\"t1\",\"additionalAssignees\":[]}", Options)!;

        Assert.Empty(ticket.AdditionalAssignees);
    }
}
