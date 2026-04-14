using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Covers the specific LinkAssetToTicket bug fix in R3.4 — the old payload
/// sent a {ticketId} key while the trudesk v2 endpoint reads {ticketUid}.
/// </summary>
public class LinkAssetToTicketTests
{
    private readonly RecordingHandler _handler = new();
    private readonly TrueDeskApiService _sut;

    public LinkAssetToTicketTests()
    {
        var httpClient = new HttpClient(_handler);
        var settings = new AppSettings { ApiBaseUrl = "https://host.test/api/v1", ConnectionTimeoutSeconds = 30 };
        var jsRuntime = Substitute.For<IJSRuntime>();
        var localStorage = new LocalStorageService(jsRuntime);
        _sut = new TrueDeskApiService(httpClient, settings, localStorage);
    }

    [Fact]
    public async Task LinkAssetToTicketAsync_sendsTicketUidInBody()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        var ok = await _sut.LinkAssetToTicketAsync("asset-1", "1042");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, _handler.LastMethod);
        Assert.Equal("/api/v1/assets/asset-1/link-ticket", _handler.LastPath);

        using var doc = JsonDocument.Parse(_handler.LastBody);
        Assert.True(doc.RootElement.TryGetProperty("ticketUid", out var uidEl),
            "Body must contain a 'ticketUid' property (not 'ticketId')");
        Assert.Equal("1042", uidEl.GetString());
        Assert.False(doc.RootElement.TryGetProperty("ticketId", out _),
            "Legacy 'ticketId' key must not be present");
    }

    [Fact]
    public async Task LinkAssetToTicketAsync_returnsFalseForEmptyAssetId()
    {
        var ok = await _sut.LinkAssetToTicketAsync("", "1042");
        Assert.False(ok);
        Assert.Null(_handler.LastMethod);
    }

    [Fact]
    public async Task LinkAssetToTicketAsync_returnsFalseForEmptyTicketUid()
    {
        var ok = await _sut.LinkAssetToTicketAsync("asset-1", "");
        Assert.False(ok);
        Assert.Null(_handler.LastMethod);
    }

    [Fact]
    public async Task LinkAssetToTicketAsync_returnsFalseOn404()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"Ticket not found\"}")
        };

        var ok = await _sut.LinkAssetToTicketAsync("asset-1", "9999");
        Assert.False(ok);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK) { Content = new StringContent("{}") };
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
