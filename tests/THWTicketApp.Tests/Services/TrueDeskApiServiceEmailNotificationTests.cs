using System.Net;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// #mailnotify: e-mail notification switches live on the server (v2 only),
/// whatever API version the app is configured for.
/// </summary>
public class TrueDeskApiServiceEmailNotificationTests
{
    private const string Path = "/api/v2/accounts/me/notification-settings";
    private readonly CapturingHttpMessageHandler _handler = new();

    private TrueDeskApiService Build()
    {
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        return new TrueDeskApiService(new HttpClient(_handler), settings, new InMemoryLocalStorageService(),
            Substitute.For<IJSRuntime>(), Substitute.For<IIndexedDbService>());
    }

    [Fact]
    public async Task Get_reads_switches_and_mail_availability()
    {
        _handler.RespondTo(HttpMethod.Get, Path, HttpStatusCode.OK,
            "{\"success\":true,\"email\":{\"personal\":false,\"newTickets\":true},\"mailEnabled\":true}");

        var result = await Build().GetEmailNotificationSettingsAsync();

        Assert.Equal(new EmailNotificationSettings(Personal: false, NewTickets: true, MailEnabled: true), result);
    }

    [Fact]
    public async Task Missing_switches_count_as_off()
    {
        _handler.RespondTo(HttpMethod.Get, Path, HttpStatusCode.OK,
            "{\"success\":true,\"email\":{},\"mailEnabled\":true}");

        var result = await Build().GetEmailNotificationSettingsAsync();

        Assert.Equal(new EmailNotificationSettings(Personal: false, NewTickets: false, MailEnabled: true), result);
    }

    [Fact]
    public async Task Get_returns_null_when_the_server_does_not_know_the_endpoint()
    {
        _handler.RespondTo(HttpMethod.Get, Path, HttpStatusCode.NotFound, "{\"success\":false}");

        Assert.Null(await Build().GetEmailNotificationSettingsAsync());
    }

    [Fact]
    public async Task Update_sends_only_the_changed_switch()
    {
        _handler.RespondTo(HttpMethod.Put, Path, HttpStatusCode.OK,
            "{\"success\":true,\"email\":{\"personal\":true,\"newTickets\":true},\"mailEnabled\":true}");

        var result = await Build().UpdateEmailNotificationSettingsAsync(personal: null, newTickets: true);

        Assert.Equal(new EmailNotificationSettings(true, true, true), result);
        Assert.Equal("{\"email\":{\"newTickets\":true}}", _handler.RequestBodies.Single());
    }

    [Fact]
    public async Task Update_returns_null_on_error()
    {
        _handler.RespondTo(HttpMethod.Put, Path, HttpStatusCode.BadRequest, "{\"success\":false}");

        Assert.Null(await Build().UpdateEmailNotificationSettingsAsync(personal: false, newTickets: null));
    }
}
