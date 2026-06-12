using System.Text.Json;
using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class RecurringTasksTests
{
    // -----------------------------------------------------------------
    // BuildPayload
    // -----------------------------------------------------------------

    [Fact]
    public void BuildPayload_includesAllBaseFields()
    {
        var payload = RecurringTasks.BuildPayload(
            "Wartung", "Beschreibung", "Betreff", "Inhalt", "monthly", 15, 7, true);

        Assert.Equal("Wartung", payload["name"]);
        Assert.Equal("Beschreibung", payload["description"]);
        Assert.Equal("Betreff", payload["ticketSubject"]);
        Assert.Equal("Inhalt", payload["ticketIssue"]);
        Assert.Equal("monthly", payload["scheduleType"]);
        Assert.Equal(15, payload["dayOfMonth"]);
        Assert.Equal(7, payload["daysBeforeDeadline"]);
        Assert.Equal(true, payload["enabled"]);
    }

    [Fact]
    public void BuildPayload_typeAndPriority_includedWhenSet()
    {
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true,
            "type-1", "prio-1");

        Assert.Equal("type-1", payload["ticketType"]);
        Assert.Equal("prio-1", payload["ticketPriority"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPayload_blankTypeAndPriority_sentAsExplicitNull(string? blank)
    {
        // The trudesk update controller only skips undefined keys — an
        // explicit null is required to unset a previously stored ref.
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true,
            blank, blank);

        Assert.True(payload.ContainsKey("ticketType"));
        Assert.Null(payload["ticketType"]);
        Assert.True(payload.ContainsKey("ticketPriority"));
        Assert.Null(payload["ticketPriority"]);
    }

    [Fact]
    public void BuildPayload_checklist_wrapsTitlesAsObjects()
    {
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true,
            checklist: new List<string> { "Eins", "Zwei" });

        var items = Assert.IsType<List<Dictionary<string, object?>>>(payload["checklist"]);
        Assert.Equal(2, items.Count);
        Assert.Equal("Eins", items[0]["title"]);
        Assert.Equal("Zwei", items[1]["title"]);
    }

    [Fact]
    public void BuildPayload_noChecklist_sendsEmptyArray()
    {
        // The key must always be present: the server replaces the whole
        // array, so omitting it would make "delete all items" not stick.
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true);

        var items = Assert.IsType<List<Dictionary<string, object?>>>(payload["checklist"]);
        Assert.Empty(items);
    }

    // -----------------------------------------------------------------
    // RecurringTask response parsing (page deserializes via
    // JsonHelper.DeserializeWrappedArray, same as LoadTasksAsync)
    // -----------------------------------------------------------------

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static RecurringTask[] Parse(string json) =>
        JsonHelper.DeserializeWrappedArray<RecurringTask>(json, "recurringTasks", Options);

    [Fact]
    public void Parse_populatedTypeAndPriority_extractsIdAndName()
    {
        const string json = """
        {
            "recurringTasks": [{
                "_id":"r1","name":"Wartung",
                "ticketType":{"_id":"t1","name":"Vorfall"},
                "ticketPriority":{"_id":"p1","name":"High"}
            }]
        }
        """;

        var tasks = Parse(json);

        Assert.Single(tasks);
        Assert.Equal("t1", tasks[0].TicketTypeId);
        Assert.Equal("Vorfall", tasks[0].TicketTypeName);
        Assert.Equal("p1", tasks[0].TicketPriorityId);
        Assert.Equal("High", tasks[0].TicketPriorityName);
    }

    [Fact]
    public void Parse_refsAsBareStrings_stillExtractsIds()
    {
        const string json = """
        {"recurringTasks":[{"_id":"r1","name":"W","ticketType":"t1","ticketPriority":"p1"}]}
        """;

        var tasks = Parse(json);

        Assert.Equal("t1", tasks[0].TicketTypeId);
        Assert.Null(tasks[0].TicketTypeName);
        Assert.Equal("p1", tasks[0].TicketPriorityId);
    }

    [Fact]
    public void Parse_nullAndMissingRefs_yieldNullIds()
    {
        const string json = """
        {"recurringTasks":[{"_id":"r1","name":"W","ticketType":null}]}
        """;

        var tasks = Parse(json);

        Assert.Null(tasks[0].TicketTypeId);
        Assert.Null(tasks[0].TicketPriorityId);
    }

    [Fact]
    public void Parse_checklist_deserializesItems()
    {
        const string json = """
        {
            "recurringTasks": [{
                "_id":"r1","name":"W",
                "checklist":[{"_id":"c1","title":"Fahrzeug prüfen"},{"title":"Material zählen"}]
            }]
        }
        """;

        var tasks = Parse(json);

        Assert.Equal(2, tasks[0].Checklist.Count);
        Assert.Equal("Fahrzeug prüfen", tasks[0].Checklist[0].Title);
        Assert.Equal("Material zählen", tasks[0].Checklist[1].Title);
    }

    [Fact]
    public void Parse_missingChecklist_returnsEmptyList()
    {
        // Tasks created before the checklist feature have no key at all.
        const string json = """{"recurringTasks":[{"_id":"r1","name":"W"}]}""";

        var tasks = Parse(json);

        Assert.Empty(tasks[0].Checklist);
    }

    [Fact]
    public void Parse_populatedGroupAssigneeAndTags_doesNotThrow()
    {
        // trudesk's getAll() populates ALL refs — make sure none of them
        // break deserialization even though the page doesn't use them yet.
        const string json = """
        {
            "recurringTasks": [{
                "_id":"r1","name":"W",
                "ticketGroup":{"_id":"g1","name":"OV"},
                "ticketAssignee":{"_id":"u1","fullname":"Max"},
                "ticketTags":[{"_id":"tag1","name":"Wartung"},"tag2"]
            }]
        }
        """;

        var tasks = Parse(json);

        Assert.Equal("g1", tasks[0].TicketGroupId);
        Assert.Equal("OV", tasks[0].TicketGroupName);
        Assert.Equal("u1", tasks[0].TicketAssigneeId);
        Assert.Equal(new List<string> { "tag1", "tag2" }, tasks[0].TicketTagIds);
    }
}
