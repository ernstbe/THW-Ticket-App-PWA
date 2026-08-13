using System.Net;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Tests the server-verified passkey lock/unlock flow (#205 — replaces the
/// old client-only scheme tested here before: that version moved the still-
/// valid session token to a differently-named localStorage key and skipped
/// the server /logout call entirely, so "unlock" was just replaying a
/// stashed credential rather than proving anything.
///
///   - LogoutAsync always calls /logout now, regardless of whether a
///     passkey is registered.
///   - When the account has at least one server-side WebAuthn credential,
///     LogoutAsync additionally remembers the USERNAME only (never a
///     token) so the login screen can offer a passkey button.
///   - VerifyWebauthnAuthenticationAsync (the actual "unlock") performs a
///     real login: it sets up auth state from a token the server minted
///     after checking a signed assertion, exactly like AuthenticateAsync.
///   - Stale keys from the pre-#205 client-only scheme are swept on
///     logout/login so they can't be misread by old code paths.
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
        _handler.RespondTo(HttpMethod.Post, "/api/v1/login", HttpStatusCode.OK,
            $"{{\"accessToken\":\"{token}\",\"user\":{{\"_id\":\"{userId}\"}}}}");
        var ok = await _sut.AuthenticateAsync(username, "pw");
        Assert.True(ok);
    }

    private void RespondWithCredentials(int count)
    {
        var creds = count > 0 ? "[{\"credentialId\":\"c1\",\"deviceLabel\":\"Test\"}]" : "[]";
        _handler.RespondTo(HttpMethod.Get, "/api/v2/webauthn/credentials", HttpStatusCode.OK,
            $"{{\"success\":true,\"credentials\":{creds}}}");
    }

    [Fact]
    public async Task LogoutAsync_withPasskey_stillCallsServerLogoutAndStashesUsernameOnly()
    {
        await LoginSuccess(token: "tk-abc", username: "alice");
        RespondWithCredentials(1);
        _handler.Requests.Clear();

        await _sut.LogoutAsync();

        // Server /logout WAS called — no more silent client-only "lock".
        Assert.Contains(_handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/v1/logout");

        // Only the username survives, never a token.
        Assert.Equal("alice", _storage.Store["last_passkey_username"]);
        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
        Assert.False(_storage.Store.ContainsKey("auth_token"));
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LogoutAsync_withoutPasskey_doesNotStashUsername()
    {
        await LoginSuccess(token: "tk-abc", username: "alice");
        RespondWithCredentials(0);
        _handler.Requests.Clear();

        await _sut.LogoutAsync();

        Assert.Contains(_handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/v1/logout");
        Assert.False(_storage.Store.ContainsKey("last_passkey_username"));
    }

    [Fact]
    public async Task LogoutAsync_sweepsStaleKeysFromThePreServerVerifiedScheme()
    {
        _storage.Store["locked_auth_token"] = "tk-stale";
        _storage.Store["locked_auth_refresh_token"] = "rt-stale";
        _storage.Store["locked_auth_username"] = "bob";
        _storage.Store["locked_auth_userid"] = "u-old";
        _storage.Store["passkey_credential_id"] = "cred-xyz";
        _storage.Store["passkey_user_id"] = "u-old";
        _storage.Store["passkey_user_name"] = "bob";
        RespondWithCredentials(0);

        await _sut.LogoutAsync();

        Assert.False(_storage.Store.ContainsKey("locked_auth_token"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_refresh_token"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_username"));
        Assert.False(_storage.Store.ContainsKey("locked_auth_userid"));
        Assert.False(_storage.Store.ContainsKey("passkey_credential_id"));
        Assert.False(_storage.Store.ContainsKey("passkey_user_id"));
        Assert.False(_storage.Store.ContainsKey("passkey_user_name"));
    }

    [Fact]
    public async Task AuthenticateAsync_dropsStaleLockedKeysFromPreviousUser()
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
    public async Task VerifyWebauthnAuthenticationAsync_onServerSuccess_establishesRealSession()
    {
        // header.payload.sig — payload base64url of {"user":{"_id":"u-new"}}
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJ1c2VyIjp7Il9pZCI6InUtbmV3In19.x";
        _handler.RespondTo(HttpMethod.Post, "/api/v2/webauthn/auth/verify", HttpStatusCode.OK,
            $"{{\"success\":true,\"token\":\"{jwt}\",\"refreshToken\":\"rt-new\"}}");

        var ok = await _sut.VerifyWebauthnAuthenticationAsync("alice", "{\"id\":\"cred-1\"}");

        Assert.True(ok);
        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("alice", _sut.CurrentUsername);
        Assert.Equal("u-new", _sut.CurrentUserId);
        Assert.Equal(jwt, _storage.Store["auth_token"]);
        Assert.Equal("alice", _storage.Store["last_passkey_username"]);
    }

    [Fact]
    public async Task VerifyWebauthnAuthenticationAsync_onServerRejection_doesNotAuthenticate()
    {
        _handler.RespondTo(HttpMethod.Post, "/api/v2/webauthn/auth/verify", HttpStatusCode.Unauthorized,
            "{\"success\":false,\"error\":\"Invalid Credential\"}");

        var ok = await _sut.VerifyWebauthnAuthenticationAsync("alice", "{\"id\":\"cred-1\"}");

        Assert.False(ok);
        Assert.False(_sut.IsAuthenticated);
        Assert.False(_storage.Store.ContainsKey("auth_token"));
    }
}
