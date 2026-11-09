using System.Net;
using System.Text;

namespace UclAggregator.Tests.Support;

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}

internal static class TestHttp
{
    public static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, string baseUrl = "https://api.test/") =>
        new(new StubHandler(respond)) { BaseAddress = new Uri(baseUrl) };

    public static HttpClient Throwing(Exception ex, string baseUrl = "https://api.test/") =>
        new(new ThrowingHandler(ex)) { BaseAddress = new Uri(baseUrl) };

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Fail(HttpStatusCode status = HttpStatusCode.InternalServerError) =>
        new(status) { Content = new StringContent("fail") };

    public static bool Has(HttpRequestMessage request, string part) =>
        request.RequestUri?.ToString().Contains(part, StringComparison.OrdinalIgnoreCase) == true;

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(ex);
    }
}
