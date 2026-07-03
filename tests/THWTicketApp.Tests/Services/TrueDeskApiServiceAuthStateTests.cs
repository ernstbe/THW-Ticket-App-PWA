using System.Net;
using System.Reflection;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Auth-state hardening on lock/logout/login (issues #208, #217).
/// </summary>
public class TrueDeskApiServiceAuthStateTests
{
    private readonly CapturingHttpMessageHandler _handler = new();
    private readonly IIndexedDbService _db = Substitute.For<IIndexedDbService>();
    private readonly InMemoryLocalStorageService _storage = new();

    private TrueDeskApiService Build()
    {
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        return new TrueDeskApiService(new HttpClient(_handler), settings, _storage, Substitute.For<IJSRuntime>(), _db);
    }

    private static void SetPrivate(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    private static object? GetPrivate(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);

    [Fact]
    public async Task Logout_purges_offline_cache()
    {
        _handler.SetDefault(HttpStatusCode.OK);
        var sut = Build();
        SetPrivate(sut, "_authToken", "tok"); // IsAuthenticated; no passkey stored -> full logout

        await sut.LogoutAsync();

        await _db.Received(1).ClearTicketCacheAsync();
        await _db.Received(1).ClearPendingActionsAsync();
        await _db.Received(1).ClearSyncLogAsync();
    }

    [Fact]
    public async Task Login_as_different_user_purges_previous_users_cache()
    {
        await _storage.SetItemAsync("auth_userid", "userA");
        _handler.RespondTo(HttpMethod.Post, "/api/v1/login", HttpStatusCode.OK,
            "{\"accessToken\":\"tok\",\"user\":{\"_id\":\"userB\"}}");
        var sut = Build();

        Assert.True(await sut.AuthenticateAsync("user", "pass"));
        await _db.Received(1).ClearTicketCacheAsync();
    }

    [Fact]
    public async Task Login_as_same_user_keeps_cache()
    {
        await _storage.SetItemAsync("auth_userid", "userA");
        _handler.RespondTo(HttpMethod.Post, "/api/v1/login", HttpStatusCode.OK,
            "{\"accessToken\":\"tok\",\"user\":{\"_id\":\"userA\"}}");
        var sut = Build();

        Assert.True(await sut.AuthenticateAsync("user", "pass"));
        await _db.DidNotReceive().ClearTicketCacheAsync();
    }

    [Fact]
    public async Task Passkey_lock_clears_cached_admin_flag()
    {
        await _storage.SetItemAsync("passkey_credential_id", "cred-1"); // -> lock branch
        var sut = Build();
        SetPrivate(sut, "_authToken", "tok");
        SetPrivate(sut, "_isAdminCached", true);

        await sut.LogoutAsync();

        Assert.Null(GetPrivate(sut, "_isAdminCached"));
    }
}
