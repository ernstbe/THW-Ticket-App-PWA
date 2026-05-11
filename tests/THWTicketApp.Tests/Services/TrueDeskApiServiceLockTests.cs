using System.Net;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Tests the lock / unlock flow added in #65:
///
///   - LogoutAsync when a passkey is registered preserves the session
///     token under locked_auth_* keys (does NOT call /logout server-side).
///   - LogoutAsync without a passkey clears everything AND calls /logout.
///   - TryUnlockSessionAsync moves locked_auth_* back to auth_* and
///     re-authenticates the in-memory state.
///   - AuthenticateAsync clears stale locked_auth_* keys.
/// </summary>
public class TrueDeskApiServiceLockTests
{
    private readonly CapturingHttpMessageHandler _handler = new();
    private readonly InMemoryLocalStorageService _storage = new();
    private readonly TrueDeskApiService _sut;

    public TrueDeskApiServiceLockTests()
    {
        var httpClient = new HttpClient(_handler);
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        var jsRuntime = Substitute.For<IJSRuntime>();
        _sut = new TrueDeskApiService(httpClient, settings, _storage, jsRuntime);
    }

    private async Task LoginSuccess(string token = "tk-abc", string userId = "u1", string username = "alice")
    {
        _handler.SetDefault(HttpStatusCode.OK, $"{{\"accessToken\":\"{token}\",\"user\":{{\"_id\":\"{userId}\"}}}}");
        var ok = await _sut.AuthenticateAsync(username, "pw");
        Assert.True(ok);
    }

    [Fact]
    public async Task LogoutAsync_withPasskey_movesTokensToLockedKeysAndSkipsServerLogout()
    {
        await LoginSuccess(token: "tk-abc");
        _storage.Store["passkey_credential_id"] = "cred-xyz";
        _handler.Requests.Clear();

        await _sut.LogoutAsync();

        // No server-side /logout call — token stays valid for unlock.
        Assert.Empty(_handler.Requests);

        // auth_* moved to locked_auth_*.
        Assert.Equal("tk-abc", _storage.Store["locked_auth_token"]);
        Assert.False(_storage.Store.ContainsKey("auth_token"));

        // In-memory state cleared.
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LogoutAsync_withoutPasskey_clearsEverythingAndCallsServerLogout()
    {
        await LoginSuccess(token: "tk-abc");
        _handler.Requests.Clear();

        await _sut.LogoutAsync();

        // Server /logout was called.
        Assert.Single(_handler.Requests);
        Assert.Equal("/api/v1/logout", _handler.Requests[0].RequestUri!.AbsolutePath);

        // All auth_* and locked_auth_* keys are gone.
        Assert.False(_storage.Store.ContainsKey("auth_token"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task TryUnlockSessionAsync_movesLockedKeysBackToAuthKeys()
    {
        // Simulate a locked session — what LogoutAsync would have left.
        _storage.Store["locked_auth_token"] = "tk-locked";
        _storage.Store["locked_auth_username"] = "alice";
        _storage.Store["locked_auth_userid"] = "u1";
        _storage.Store["passkey_credential_id"] = "cred-xyz";

        var restored = await _sut.TryUnlockSessionAsync();

        Assert.True(restored);
        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("alice", _sut.CurrentUsername);
        Assert.Equal("u1", _sut.CurrentUserId);

        // locked_auth_* removed, auth_* set.
        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
        Assert.Equal("tk-locked", _storage.Store["auth_token"]);
        Assert.Equal("alice", _storage.Store["auth_username"]);
    }

    [Fact]
    public async Task TryUnlockSessionAsync_withoutLockedKeys_fallsBackToNormalRestore()
    {
        // Edge case: passkey button used on a never-locked session.
        _storage.Store["auth_token"] = "tk-normal";
        _storage.Store["auth_username"] = "alice";
        _storage.Store["auth_userid"] = "u1";

        var restored = await _sut.TryUnlockSessionAsync();

        Assert.True(restored);
        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("tk-normal", _storage.Store["auth_token"]);
    }

    [Fact]
    public async Task TryUnlockSessionAsync_withNothingStored_returnsFalse()
    {
        // The "old passkey, fresh device" failure mode the user hit.
        // Documented in PR #69 with a clearer error message.
        var restored = await _sut.TryUnlockSessionAsync();

        Assert.False(restored);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_dropsStaleLockedKeys()
    {
        // Stale locked tokens from a previous user must not survive a
        // fresh password login — otherwise the new user's biometric
        // unlock could resurrect the old user's session.
        _storage.Store["locked_auth_token"] = "tk-stale";
        _storage.Store["locked_auth_userid"] = "u-old";

        await LoginSuccess(token: "tk-new", userId: "u-new", username: "bob");

        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_userid"));
        Assert.Equal("tk-new", _storage.Store["auth_token"]);
    }

    [Fact]
    public async Task LogoutAsync_passkeyRegisteredButNotAuthenticated_doesNotLock()
    {
        // If the user isn't actually logged in, lock-on-logout is a no-op.
        // (LogoutAsync still cleans up to be safe.)
        _storage.Store["passkey_credential_id"] = "cred-xyz";

        await _sut.LogoutAsync();

        Assert.False(_storage.Store.ContainsKey("auth_token"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
    }
}
