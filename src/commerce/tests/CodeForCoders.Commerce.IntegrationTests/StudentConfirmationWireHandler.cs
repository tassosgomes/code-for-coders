using System.Net;
using System.Net.Http.Json;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class StudentConfirmationWireHandler(object body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
}
