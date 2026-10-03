using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Contracts;
using Polly.Timeout;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyIdentityHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string? Code { get; set; }
    public bool Timeout { get; set; }
    public bool Unavailable { get; set; }
    public int Calls { get; private set; }
    public string? Assertion { get; private set; }
    public string? Session { get; private set; }
    public string? Url { get; private set; }
    public StudentAccountLookupRequestV1? Input { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Assert.Equal(HttpMethod.Post, request.Method);
        Url = request.RequestUri!.AbsoluteUri;
        Assertion = request.Headers.Authorization?.Parameter;
        Session = request.Headers.GetValues("X-Staff-Session").Single();
        Input = await request.Content!.ReadFromJsonAsync<StudentAccountLookupRequestV1>(cancellationToken);
        if (Timeout) throw new TimeoutRejectedException();
        if (Unavailable) throw new HttpRequestException("Identity connection failed.");
        return new HttpResponseMessage(Status)
        {
            Content = Status == HttpStatusCode.OK
            ? JsonContent.Create(new StudentAccountV1(Guid.Parse("00000000-0000-7000-8000-000000000099"), Input!.Email!, "Lookup Student", false, "active"))
            : JsonContent.Create(new { code = Code })
        };
    }
}
