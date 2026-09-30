using System.Net;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CourseJwksHandler(string document) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(document) });
}
