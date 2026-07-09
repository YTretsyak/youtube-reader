using System.Net;

namespace Infrastructure.Tests.TestDoubles;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public static FakeHttpMessageHandler ReturningStatus(HttpStatusCode statusCode, string? content = null) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = content is null ? null : new StringContent(content)
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_respond(request));
}
