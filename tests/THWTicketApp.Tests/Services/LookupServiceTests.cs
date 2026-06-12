using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

public class LookupServiceTests
{
    private const string TypesJson = """
    [
        {
            "_id": "t1", "name": "Task",
            "priorities": [
                { "_id": "p1", "name": "Normal" },
                { "_id": "p2", "name": "High" }
            ]
        },
        {
            "_id": "t2", "name": "Issue",
            "priorities": [
                { "_id": "p2", "name": "High" },
                { "_id": "p3", "name": "Very High" }
            ]
        }
    ]
    """;

    private static (LookupService Sut, ITrueDeskApiService Api) Create(string json = TypesJson)
    {
        var api = Substitute.For<ITrueDeskApiService>();
        api.GetTicketTypesAsync().Returns(json);
        return (new LookupService(api), api);
    }

    [Fact]
    public async Task ReturnsTypes_andDeduplicatedPriorityUnion()
    {
        var (sut, _) = Create();

        var (types, priorities) = await sut.GetTypesAndPrioritiesAsync();

        Assert.Equal(new[] { "t1", "t2" }, types.Select(t => t.Id));
        // p2 is attached to both types — the union must contain it once.
        Assert.Equal(new[] { "p1", "p2", "p3" }, priorities.Select(p => p.Id));
    }

    [Fact]
    public async Task DoesNotMutateNames_translationStaysInTranslatedName()
    {
        var (sut, _) = Create();

        var (_, priorities) = await sut.GetTypesAndPrioritiesAsync();

        // Raw server names survive; the German label only exists as the
        // computed TranslatedName (generic Translator covers "Very High",
        // which the old per-page TranslatePriority table did not).
        var veryHigh = priorities.Single(p => p.Id == "p3");
        Assert.Equal("Very High", veryHigh.Name);
        Assert.Equal("Sehr Hoch", veryHigh.TranslatedName);
    }

    [Fact]
    public async Task SecondCall_servedFromCache_singleApiCall()
    {
        var (sut, api) = Create();

        var first = await sut.GetTypesAndPrioritiesAsync();
        var second = await sut.GetTypesAndPrioritiesAsync();

        await api.Received(1).GetTicketTypesAsync();
        Assert.Same(first.Types, second.Types);
    }

    [Fact]
    public async Task FailedLoad_isNotCached_nextCallRetries()
    {
        var api = Substitute.For<ITrueDeskApiService>();
        api.GetTicketTypesAsync().Returns(
            _ => Task.FromException<string>(new HttpRequestException("offline")),
            _ => Task.FromResult(TypesJson));
        var sut = new LookupService(api);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetTypesAndPrioritiesAsync());
        var (types, _) = await sut.GetTypesAndPrioritiesAsync();

        Assert.Equal(2, types.Count);
        await api.Received(2).GetTicketTypesAsync();
    }

    [Fact]
    public async Task Reset_dropsCache_nextCallRefetches()
    {
        var (sut, api) = Create();
        await sut.GetTypesAndPrioritiesAsync();

        sut.Reset();
        await sut.GetTypesAndPrioritiesAsync();

        await api.Received(2).GetTicketTypesAsync();
    }

    [Fact]
    public async Task Logout_dropsCache()
    {
        var (sut, api) = Create();
        await sut.GetTypesAndPrioritiesAsync();

        api.LoggingOut += Raise.Event<Func<Task>>();
        await sut.GetTypesAndPrioritiesAsync();

        await api.Received(2).GetTicketTypesAsync();
    }

    [Fact]
    public async Task EmptyOrNullPayload_yieldsEmptyLists()
    {
        var (sut, _) = Create("null");

        var (types, priorities) = await sut.GetTypesAndPrioritiesAsync();

        Assert.Empty(types);
        Assert.Empty(priorities);
    }
}
