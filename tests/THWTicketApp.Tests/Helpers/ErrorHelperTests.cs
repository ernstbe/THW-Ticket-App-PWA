using System.Net;
using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class ErrorHelperTests
{
    [Fact]
    public void Categorize_Unauthorized_ReturnsSessionExpired()
    {
        var ex = new HttpRequestException("", null, HttpStatusCode.Unauthorized);
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Sitzung abgelaufen", message);
        Assert.False(canRetry);
    }

    [Fact]
    public void Categorize_Forbidden_ReturnsNoPermission()
    {
        var ex = new HttpRequestException("", null, HttpStatusCode.Forbidden);
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Berechtigung", message);
        Assert.False(canRetry);
    }

    [Fact]
    public void Categorize_NotFound_ReturnsNotFound()
    {
        var ex = new HttpRequestException("", null, HttpStatusCode.NotFound);
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("nicht gefunden", message);
        Assert.False(canRetry);
    }

    [Fact]
    public void Categorize_ServerError_ReturnsRetryable()
    {
        var ex = new HttpRequestException("", null, HttpStatusCode.InternalServerError);
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Serverfehler", message);
        Assert.True(canRetry);
    }

    [Fact]
    public void Categorize_GenericHttpError_ReturnsConnectionError()
    {
        var ex = new HttpRequestException("connection refused");
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Verbindung", message);
        Assert.True(canRetry);
    }

    [Fact]
    public void Categorize_Timeout_ReturnsTimeoutMessage()
    {
        var ex = new TaskCanceledException();
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Zeitüberschreitung", message);
        Assert.True(canRetry);
    }

    [Fact]
    public void Categorize_JsonError_ReturnsFormatError()
    {
        var ex = new System.Text.Json.JsonException("bad json");
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("Datenformat", message);
        Assert.False(canRetry);
    }

    [Fact]
    public void Categorize_UnknownException_ReturnsGenericRetryable()
    {
        var ex = new InvalidOperationException("something");
        var (message, canRetry) = ErrorHelper.Categorize(ex);
        Assert.Contains("unerwarteter Fehler", message);
        Assert.True(canRetry);
    }
}
