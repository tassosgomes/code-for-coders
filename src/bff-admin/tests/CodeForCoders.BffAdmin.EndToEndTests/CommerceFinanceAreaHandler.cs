using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class CommerceFinanceAreaHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    public string? LastAccessToken { get; private set; }

    public string? LastPath { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastAccessToken = request.Headers.Authorization?.Parameter;
        LastPath = request.RequestUri?.AbsolutePath;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new FinanceAreaResponse("reserved")),
        });
    }

    public void Reset()
    {
        RequestCount = 0;
        LastAccessToken = null;
        LastPath = null;
    }
}
