using System.Net;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// #invite: invitation and password-reset links go to the v2 public
/// endpoints regardless of the configured API version.
/// </summary>
public class TrueDeskApiServiceAccountLinkTests
{
    private const string Token = "AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abcde";
    private readonly CapturingHttpMessageHandler _handler = new();

    private TrueDeskApiService Build()
    {
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        return new TrueDeskApiService(new HttpClient(_handler), settings, new InMemoryLocalStorageService(),
            Substitute.For<IJSRuntime>(), Substitute.For<IIndexedDbService>());
    }

    [Fact]
    public async Task Check_invite_returns_account_from_v2_public_endpoint()
    {
        _handler.RespondTo(HttpMethod.Get, $"/api/v2/public/invite/{Token}", HttpStatusCode.OK,
            "{\"success\":true,\"account\":{\"fullname\":\"Erika Muster\",\"username\":\"erika\"}}");

        var result = await Build().CheckAccountLinkAsync(isInvite: true, Token);

        Assert.Equal(("Erika Muster", "erika"), result);
    }

    [Fact]
    public async Task Check_returns_null_for_invalid_link()
    {
        _handler.RespondTo(HttpMethod.Get, $"/api/v2/public/password-reset/{Token}", HttpStatusCode.NotFound,
            "{\"success\":false,\"error\":\"Der Link ist ungültig oder abgelaufen.\"}");

        Assert.Null(await Build().CheckAccountLinkAsync(isInvite: false, Token));
    }

    [Fact]
    public async Task Accept_posts_both_passwords_and_returns_username()
    {
        _handler.RespondTo(HttpMethod.Post, $"/api/v2/public/password-reset/{Token}", HttpStatusCode.OK,
            "{\"success\":true,\"username\":\"erika\"}");

        var result = await Build().AcceptAccountLinkAsync(isInvite: false, Token, "Geheim-123", "Geheim-123");

        Assert.True(result.Success);
        Assert.Equal("erika", result.Username);
        Assert.False(result.LinkInvalid);
        Assert.Contains("\"password\":\"Geheim-123\"", _handler.RequestBodies.Single());
        Assert.Contains("\"passwordConfirm\":\"Geheim-123\"", _handler.RequestBodies.Single());
    }

    [Fact]
    public async Task Accept_surfaces_server_validation_message()
    {
        _handler.RespondTo(HttpMethod.Post, $"/api/v2/public/invite/{Token}", HttpStatusCode.BadRequest,
            "{\"success\":false,\"error\":\"Das Passwort muss mindestens 8 Zeichen lang sein.\"}");

        var result = await Build().AcceptAccountLinkAsync(isInvite: true, Token, "kurz", "kurz");

        Assert.False(result.Success);
        Assert.False(result.LinkInvalid);
        Assert.Equal("Das Passwort muss mindestens 8 Zeichen lang sein.", result.Error);
    }

    [Fact]
    public async Task Accept_flags_used_or_expired_link()
    {
        _handler.RespondTo(HttpMethod.Post, $"/api/v2/public/invite/{Token}", HttpStatusCode.NotFound,
            "{\"success\":false,\"error\":\"Der Link ist ungültig oder abgelaufen.\"}");

        var result = await Build().AcceptAccountLinkAsync(isInvite: true, Token, "Geheim-123", "Geheim-123");

        Assert.False(result.Success);
        Assert.True(result.LinkInvalid);
    }

    [Fact]
    public async Task Request_reset_posts_email_to_v2()
    {
        _handler.RespondTo(HttpMethod.Post, "/api/v2/public/password-reset", HttpStatusCode.OK, "{\"success\":true}");

        Assert.True(await Build().RequestPasswordResetAsync("erika@example.org"));
        Assert.Equal("https://host.test/api/v2/public/password-reset", _handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains("erika@example.org", _handler.RequestBodies.Single());
    }

    [Fact]
    public async Task Request_reset_reports_rate_limit_as_failure()
    {
        _handler.RespondTo(HttpMethod.Post, "/api/v2/public/password-reset", HttpStatusCode.TooManyRequests);

        Assert.False(await Build().RequestPasswordResetAsync("erika@example.org"));
    }
}
