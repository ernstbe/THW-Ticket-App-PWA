using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Models;

/// <summary>
/// Follow-up to the PopulatedRef migration (PR #167): Ticket, Owner,
/// Assignee and Group used to carry setter-only JsonElement "absorber"
/// properties for fields trudesk may return either populated (object) or
/// as bare ObjectId strings (subscribers, role, members, sendMailTo).
/// Those backing fields were never read anywhere, so the absorbers were
/// removed instead of migrated — System.Text.Json simply ignores the
/// unknown members. These tests pin that down: every shape trudesk sends
/// must still deserialize, and a serialize → deserialize roundtrip (the
/// shape a future offline cache would produce) must keep the data the
/// app actually uses.
/// </summary>
public class TicketSerializationTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_formerAbsorberFieldsInAllShapes_doesNotThrow()
    {
        // subscribers: populated object + bare string mixed; role: object
        // on owner, bare string on assignee; members/sendMailTo on group —
        // exactly the shapes the removed absorbers used to swallow.
        const string json = """
            {
                "_id": "t1",
                "uid": 1001,
                "subject": "Pumpe defekt",
                "subscribers": [{ "_id": "u1", "username": "anna" }, "u2"],
                "owner": { "_id": "o1", "fullname": "Otto Owner", "role": { "_id": "r1", "name": "admin" } },
                "assignee": { "_id": "a1", "fullname": "Anna Assignee", "role": "r2" },
                "group": {
                    "_id": "g1",
                    "name": "OV Emm",
                    "members": [{ "_id": "m1", "fullname": "Max" }, "m2"],
                    "sendMailTo": ["m1", { "_id": "m2" }]
                }
            }
            """;

        var ticket = JsonSerializer.Deserialize<Ticket>(json, Options)!;

        Assert.Equal("t1", ticket.Id);
        Assert.Equal("o1", ticket.Owner!.Id);
        Assert.Equal("Otto Owner", ticket.Owner.Fullname);
        Assert.Equal("a1", ticket.Assignee!.Id);
        Assert.Equal("g1", ticket.Group!.Id);
        Assert.Equal("OV Emm", ticket.Group.Name);
    }

    [Fact]
    public void Deserialize_formerAbsorberFieldsNull_doesNotThrow()
    {
        const string json = """
            {
                "_id": "t1",
                "uid": 1,
                "subscribers": null,
                "owner": { "_id": "o1", "role": null },
                "group": { "_id": "g1", "members": null, "sendMailTo": null }
            }
            """;

        var ticket = JsonSerializer.Deserialize<Ticket>(json, Options)!;

        Assert.Equal("o1", ticket.Owner!.Id);
        Assert.Equal("g1", ticket.Group!.Id);
    }

    [Fact]
    public void SerializeRoundtrip_keepsTicketData()
    {
        const string json = """
            {
                "_id": "t1",
                "uid": 1001,
                "subject": "Pumpe defekt",
                "subscribers": [{ "_id": "u1" }, "u2"],
                "owner": { "_id": "o1", "fullname": "Otto Owner", "role": { "_id": "r1", "name": "admin" } },
                "assignee": { "_id": "a1", "role": "r2" },
                "group": { "_id": "g1", "name": "OV Emm", "members": ["m1"], "sendMailTo": [] },
                "status": { "_id": "s1", "name": "Offen" },
                "dueDate": "2026-06-30T00:00:00Z"
            }
            """;

        var original = JsonSerializer.Deserialize<Ticket>(json, Options)!;
        var roundtripped = JsonSerializer.Deserialize<Ticket>(
            JsonSerializer.Serialize(original, Options), Options)!;

        Assert.Equal("t1", roundtripped.Id);
        Assert.Equal(1001, roundtripped.Uid);
        Assert.Equal("Pumpe defekt", roundtripped.Subject);
        Assert.Equal("o1", roundtripped.Owner?.Id);
        Assert.Equal("Otto Owner", roundtripped.Owner?.Fullname);
        Assert.Equal("a1", roundtripped.Assignee?.Id);
        Assert.Equal("g1", roundtripped.Group?.Id);
        Assert.Equal("OV Emm", roundtripped.Group?.Name);
        Assert.Equal("s1", roundtripped.Status?.Id);
        Assert.Equal(original.DueDate, roundtripped.DueDate);
    }

    [Fact]
    public void Serialize_doesNotEmitRemovedAbsorberMembers()
    {
        // The old absorbers serialized as literal nulls ("subscribers":
        // null, "role": null, ...). Nothing in the app re-serializes these
        // models today (the IndexedDB ticket cache stores the raw server
        // JSON string), but if something starts to, the removed members
        // must not reappear and accidentally clear server-side data.
        var ticket = new Ticket { Id = "t1", Owner = new Owner { Id = "o1" }, Group = new Group { Id = "g1" } };

        var json = JsonSerializer.Serialize(ticket, Options);

        Assert.DoesNotContain("subscribers", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("members", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sendMailTo", json, StringComparison.OrdinalIgnoreCase);
    }
}
