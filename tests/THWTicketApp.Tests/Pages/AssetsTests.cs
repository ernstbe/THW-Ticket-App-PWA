using NSubstitute;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

/// <summary>
/// R3.4 — pure-logic coverage for the link-asset-to-ticket helper.
/// The helper is the seam between the Razor UI and the API service, so
/// we can cover empty-uid, success, failure, and exception branches
/// without standing up bUnit.
/// </summary>
public class AssetsTests
{
    private readonly ITrueDeskApiService _api = Substitute.For<ITrueDeskApiService>();
    private readonly Asset _asset = new() { Id = "asset-1", Name = "Funkgerät" };

    [Fact]
    public async Task TryLinkAssetAsync_withEmptyUid_returnsEmptyUid()
    {
        var result = await Assets.TryLinkAssetAsync(_api, _asset, "");
        Assert.Equal(Assets.LinkTicketOutcome.EmptyUid, result);
        await _api.DidNotReceive().LinkAssetToTicketAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryLinkAssetAsync_withWhitespaceUid_returnsEmptyUid()
    {
        var result = await Assets.TryLinkAssetAsync(_api, _asset, "   ");
        Assert.Equal(Assets.LinkTicketOutcome.EmptyUid, result);
    }

    [Fact]
    public async Task TryLinkAssetAsync_withNullUid_returnsEmptyUid()
    {
        var result = await Assets.TryLinkAssetAsync(_api, _asset, null);
        Assert.Equal(Assets.LinkTicketOutcome.EmptyUid, result);
    }

    [Fact]
    public async Task TryLinkAssetAsync_trimsUidBeforeSending()
    {
        _api.LinkAssetToTicketAsync("asset-1", "1042").Returns(true);

        var result = await Assets.TryLinkAssetAsync(_api, _asset, "  1042  ");

        Assert.Equal(Assets.LinkTicketOutcome.Success, result);
        await _api.Received(1).LinkAssetToTicketAsync("asset-1", "1042");
    }

    [Fact]
    public async Task TryLinkAssetAsync_whenApiReturnsTrue_returnsSuccess()
    {
        _api.LinkAssetToTicketAsync("asset-1", "1042").Returns(true);
        var result = await Assets.TryLinkAssetAsync(_api, _asset, "1042");
        Assert.Equal(Assets.LinkTicketOutcome.Success, result);
    }

    [Fact]
    public async Task TryLinkAssetAsync_whenApiReturnsFalse_returnsFailed()
    {
        _api.LinkAssetToTicketAsync("asset-1", "9999").Returns(false);
        var result = await Assets.TryLinkAssetAsync(_api, _asset, "9999");
        Assert.Equal(Assets.LinkTicketOutcome.Failed, result);
    }

    [Fact]
    public async Task TryLinkAssetAsync_whenApiThrows_returnsFailed()
    {
        _api.LinkAssetToTicketAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<Task<bool>>(_ => throw new HttpRequestException("boom"));

        var result = await Assets.TryLinkAssetAsync(_api, _asset, "1042");

        Assert.Equal(Assets.LinkTicketOutcome.Failed, result);
    }
}
