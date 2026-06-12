using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSubstitute;
using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Pages;
using THWTicketApp.Web.Services;

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
            "Wartung", "Beschreibung", "Betreff", "Inhalt", "monthly", 15, 7, true,
            "type-1", "prio-1", "group-1");

        Assert.Equal("Wartung", payload["name"]);
        Assert.Equal("Beschreibung", payload["description"]);
        Assert.Equal("Betreff", payload["ticketSubject"]);
        Assert.Equal("Inhalt", payload["ticketIssue"]);
        Assert.Equal("monthly", payload["scheduleType"]);
        Assert.Equal(15, payload["dayOfMonth"]);
        Assert.Equal(7, payload["daysBeforeDeadline"]);
        Assert.Equal(true, payload["enabled"]);
        Assert.Equal("group-1", payload["ticketGroup"]);
    }

    [Fact]
    public void BuildPayload_typePriorityAndGroup_includedWhenSet()
    {
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true,
            "type-1", "prio-1", "group-1");

        Assert.Equal("type-1", payload["ticketType"]);
        Assert.Equal("prio-1", payload["ticketPriority"]);
        Assert.Equal("group-1", payload["ticketGroup"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPayload_blankRefs_keysOmitted(string? blank)
    {
        // ticketType/ticketPriority/ticketGroup are required:true on the
        // server schema — sending an explicit null fails validation with
        // a 500, so blank selections must omit the key entirely.
        var payload = RecurringTasks.BuildPayload("N", "", "S", "", "monthly", 1, 7, true,
            blank, blank, blank);

        Assert.False(payload.ContainsKey("ticketType"));
        Assert.False(payload.ContainsKey("ticketPriority"));
        Assert.False(payload.ContainsKey("ticketGroup"));
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

/// <summary>
/// bUnit tests for the dialog's template-stash behavior: clearing the
/// template picker must restore the state from when the dialog opened,
/// not wipe it (BuildPayload always sends the checklist — [] clears it
/// server-side).
/// </summary>
public class RecurringTasksComponentTests : BunitContext, IAsyncLifetime
{
    private readonly ITrueDeskApiService _api;

    public RecurringTasksComponentTests()
    {
        _api = Substitute.For<ITrueDeskApiService>();
        _api.GetTicketTypesAsync().Returns("[]");
        _api.GetTicketTemplatesAsync().Returns("""{"ticketTemplates":[]}""");
        _api.GetGroupsAsync().Returns("""{"success":true,"groups":[{"_id":"g1","name":"OV"}]}""");
        _api.GetRecurringTasksAsync().Returns("""{"recurringTasks":[]}""");
        _api.GetCurrentUserProfileAsync().Returns(Task.FromResult<UserProfile?>(null));

        var jsRuntime = Substitute.For<IJSRuntime>();
        var localStorage = new LocalStorageService(jsRuntime);

        Services.AddMudServices();
        Services.AddSingleton(_api);
        Services.AddSingleton(new LocalizationService(localStorage));
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new AlwaysAuthenticatedProvider());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // Route xUnit's cleanup through BunitContext.DisposeAsync (see
    // SyncConflictsTests for the bunit 2.x rationale).
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    [Fact]
    public async Task ClearingTemplate_onEdit_restoresOriginalChecklistAndSubject()
    {
        var cut = Render<RecurringTasks>();
        var task = new RecurringTask
        {
            Id = "r1",
            Name = "Wartung",
            TicketSubject = "Original-Betreff",
            Checklist = [new RecurringTaskChecklistItem { Title = "Original-Punkt" }]
        };

        await cut.InvokeAsync(() => cut.Instance.ShowEditDialog(task));

        var template = new TicketTemplate
        {
            Name = "Vorlage",
            Subject = "Vorlagen-Betreff",
            Checklist = ["Vorlagen-Punkt"]
        };
        await cut.InvokeAsync(() => cut.Instance.OnTemplateSelected(template));

        Assert.Equal(new[] { "Vorlagen-Punkt" }, cut.Instance.FormChecklistForTests);
        Assert.Equal("Vorlagen-Betreff", cut.Instance.FormTicketSubjectForTests);

        await cut.InvokeAsync(() => cut.Instance.OnTemplateSelected(null));

        Assert.Equal(new[] { "Original-Punkt" }, cut.Instance.FormChecklistForTests);
        Assert.Equal("Original-Betreff", cut.Instance.FormTicketSubjectForTests);
    }

    private sealed class AlwaysAuthenticatedProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test")],
                "test");
            return Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(identity)));
        }
    }
}
