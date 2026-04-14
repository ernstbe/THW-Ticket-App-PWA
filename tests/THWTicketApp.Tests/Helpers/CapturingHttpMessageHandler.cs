using System.Net;
using System.Text;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that captures every outgoing
/// request and returns canned responses by URL + method pattern. Lets service-level
/// tests assert the URL, HTTP verb, and serialized body without ever opening a socket.
/// </summary>
internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();

    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();
    private HttpResponseMessage _defaultResponse = new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

    public void SetDefault(HttpStatusCode status, string body = "{}")
    {
        _defaultResponse = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    public void RespondTo(HttpMethod method, string pathSuffix, HttpStatusCode status, string body = "{}")
    {
        _routes.Add((
            req => req.Method == method && req.RequestUri!.AbsolutePath.EndsWith(pathSuffix, StringComparison.Ordinal),
            _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
    }

    public void RespondTo(HttpMethod method, Func<Uri, bool> matcher, HttpStatusCode status, string body = "{}")
    {
        _routes.Add((
            req => req.Method == method && matcher(req.RequestUri!),
            _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        else
            RequestBodies.Add(string.Empty);

        foreach (var (match, respond) in _routes)
        {
            if (match(request))
                return respond(request);
        }
        return _defaultResponse;
    }
}
