using System.Text.Json;
using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// Regression tests for the July 2026 production outage: trudesk stores an
/// explicit <c>"dueDate": null</c> when a due date is cleared, and a single
/// such ticket aborted deserialization of the ENTIRE list — every page showed
/// "Ungültiges Datenformat vom Server". The tolerant converters map off-type
/// values to the unset-defaults, and JsonHelper skips elements that still
/// fail instead of failing the whole array.
/// </summary>
public class TolerantJsonConvertersTests
{
    // Realistic trudesk v1 ticket as the healthy baseline (uid 42);
    // individual tests mutate single fields into their poisoned variants.
    private const string HealthyTicket = """
        {
          "_id": "665f1c2a9b1e8a0012345678",
          "uid": 42,
          "subject": "Pumpe defekt",
          "issue": "<p>Text</p>",
          "date": "2026-07-01T10:00:00.000Z",
          "updated": "2026-07-10T08:30:00.000Z",
          "deleted": false,
          "__v": 3,
          "owner": { "_id": "665f0000000000000000aaaa", "fullname": "Max", "username": "max" },
          "status": { "_id": "665f0000000000000000dddd", "name": "Neu", "uid": 0, "isResolved": false },
          "priority": { "_id": "665f0000000000000000eeee", "name": "Normal" },
          "tags": [],
          "comments": [ { "_id": "c1", "comment": "hi", "date": "2026-07-02T10:00:00.000Z" } ],
          "notes": [], "attachments": [], "history": []
        }
        """;

    private static string SecondHealthyTicket => HealthyTicket
        .Replace("\"uid\": 42", "\"uid\": 43")
        .Replace("665f1c2a9b1e8a0012345678", "665f1c2a9b1e8a0012345679");

    private static string WrapAsV1List(params string[] tickets) =>
        $$"""{ "success": true, "tickets": [ {{string.Join(", ", tickets)}} ] }""";

    [Fact]
    public void DueDateNull_DoesNotKillTheList_AndMapsToMinValue()
    {
        // The exact production payload shape: one cleared due date among healthy tickets.
        var poisoned = HealthyTicket.Replace("\"uid\": 42,", "\"uid\": 42, \"dueDate\": null,");
        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(poisoned, SecondHealthyTicket), "tickets");

        Assert.Equal(2, result.Length);
        Assert.Equal(DateTime.MinValue, result[0].DueDate); // MinValue == "kein Fälligkeitsdatum"
        Assert.Equal(42, result[0].Uid);
        Assert.Equal(43, result[1].Uid);
    }

    [Theory]
    [InlineData("\"date\": \"2026-07-01T10:00:00.000Z\"", "\"date\": null")]
    [InlineData("\"updated\": \"2026-07-10T08:30:00.000Z\"", "\"updated\": null")]
    [InlineData("\"uid\": 42", "\"uid\": null")]
    [InlineData("\"deleted\": false", "\"deleted\": null")]
    [InlineData("\"__v\": 3", "\"__v\": null")]
    [InlineData("\"date\": \"2026-07-02T10:00:00.000Z\"", "\"date\": null")] // comment date
    [InlineData("\"uid\": 0, \"isResolved\": false", "\"uid\": null, \"isResolved\": null")] // nested status
    [InlineData("\"date\": \"2026-07-01T10:00:00.000Z\"", "\"date\": 1720900000000")] // unix number → MinValue
    public void PoisonedValueTypeField_ParsesInsteadOfThrowing(string find, string replace)
    {
        var poisoned = HealthyTicket.Replace(find, replace);
        Assert.NotEqual(HealthyTicket, poisoned); // mutation must have applied

        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(poisoned, SecondHealthyTicket), "tickets");

        Assert.Equal(2, result.Length);
        Assert.Equal(43, result[^1].Uid);
    }

    [Fact]
    public void UidAsNumericString_ParsesToInt()
    {
        var poisoned = HealthyTicket.Replace("\"uid\": 42", "\"uid\": \"42\"");
        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(poisoned), "tickets");

        Assert.Single(result);
        Assert.Equal(42, result[0].Uid);
    }

    [Fact]
    public void ClosedDateNullAndEmptyString_MapToNull()
    {
        var withNull = HealthyTicket.Replace("\"uid\": 42,", "\"uid\": 42, \"closedDate\": null,");
        var withEmpty = HealthyTicket.Replace("\"uid\": 42,", "\"uid\": 42, \"closedDate\": \"\",");

        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(withNull, withEmpty), "tickets");

        Assert.Equal(2, result.Length);
        Assert.Null(result[0].ClosedDate);
        Assert.Null(result[1].ClosedDate);
    }

    [Fact]
    public void StructurallyAlienElement_IsSkipped_HealthyOnesSurvive()
    {
        // A bare string can't be a Ticket even with tolerant converters — it is
        // dropped with a console warning instead of aborting the whole list.
        var result = JsonHelper.DeserializeWrappedArray<Ticket>(
            WrapAsV1List("\"garbage\"", SecondHealthyTicket), "tickets");

        Assert.Single(result);
        Assert.Equal(43, result[0].Uid);
    }

    [Fact]
    public void NullElement_IsDropped()
    {
        var result = JsonHelper.DeserializeWrappedArray<Ticket>(
            WrapAsV1List("null", SecondHealthyTicket), "tickets");

        Assert.Single(result);
        Assert.Equal(43, result[0].Uid);
    }

    [Fact]
    public void CallerSuppliedOptions_AreUpgradedToTolerant()
    {
        // Pages pass their own options instance — EnsureTolerant must add the
        // converters without mutating the caller's (possibly frozen) instance.
        var callerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var poisoned = HealthyTicket.Replace("\"uid\": 42,", "\"uid\": 42, \"dueDate\": null,");

        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(poisoned), "tickets", callerOptions);

        Assert.Single(result);
        Assert.Equal(DateTime.MinValue, result[0].DueDate);
        Assert.Empty(callerOptions.Converters);
    }

    [Fact]
    public void SingleTicketParse_WithTolerantOptions_SurvivesDueDateNull()
    {
        // TicketDetail parses the single-ticket response outside JsonHelper.
        var poisoned = HealthyTicket.Replace("\"uid\": 42,", "\"uid\": 42, \"dueDate\": null,");

        var ticket = JsonSerializer.Deserialize<Ticket>(poisoned, JsonHelper.TolerantOptions);

        Assert.NotNull(ticket);
        Assert.Equal(42, ticket.Uid);
        Assert.Equal(DateTime.MinValue, ticket.DueDate);
    }

    [Fact]
    public void HealthyPayload_RoundtripsUnchanged()
    {
        // Guard: the converters must not alter well-formed data.
        var result = JsonHelper.DeserializeWrappedArray<Ticket>(WrapAsV1List(HealthyTicket), "tickets");

        Assert.Single(result);
        var t = result[0];
        Assert.Equal(42, t.Uid);
        Assert.False(t.Deleted);
        Assert.Equal(3, t.Version);
        Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), t.Date.ToUniversalTime());
        Assert.Single(t.Comments);
    }
}
