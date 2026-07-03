using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

public class RealtimeServiceTests
{
    private static RealtimeService Build(ITrueDeskApiService? api = null) =>
        new(Substitute.For<IJSRuntime>(), new AppSettings(), new InMemoryLocalStorageService(),
            api ?? Substitute.For<ITrueDeskApiService>());

    // #202: logout must tear down the socket so the logged-out user stops
    // receiving realtime events/notifications on a shared device.
    [Fact]
    public void LoggingOut_disconnectsTheSocket()
    {
        var api = Substitute.For<ITrueDeskApiService>();
        var sut = Build(api);
        sut.OnConnected();
        Assert.True(sut.IsConnected);

        api.LoggingOut += Raise.Event<Func<Task>>();

        Assert.False(sut.IsConnected);
    }

    // #209: the numeric uid must reach TicketEvent so the notification deep-link
    // can target the /app/tickets/{uid:int} route (the Mongo _id never matches).
    [Fact]
    public void OnTicketEvent_raisesTicketEvent_withNumericUidAlongsideMongoId()
    {
        var sut = Build();
        string? gotName = null, gotId = null, gotUid = null;
        sut.TicketEvent += (name, id, uid) => { gotName = name; gotId = id; gotUid = uid; };

        sut.OnTicketEvent("ticketUpdated", "665fabcdef0123456789abcd", "1098");

        Assert.Equal("ticketUpdated", gotName);
        Assert.Equal("665fabcdef0123456789abcd", gotId);
        Assert.Equal("1098", gotUid);
    }

    [Fact]
    public void OnTicketEvent_nullUid_defaultsToEmpty()
    {
        var sut = Build();
        string? gotUid = "sentinel";
        sut.TicketEvent += (_, _, uid) => gotUid = uid;

        sut.OnTicketEvent("ticketUpdated", "id", null!);

        Assert.Equal(string.Empty, gotUid);
    }
}
