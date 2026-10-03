using System.Net;
using System.Net.Http.Json;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyCoursesHttpHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public Uri? Uri { get; private set; }
    public string? Token { get; private set; }
    public bool Timeout { get; set; }
    public bool Unavailable { get; set; }
    public bool Malformed { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Uri = request.RequestUri; Token = request.Headers.Authorization?.Parameter;
        if (Timeout) throw new TimeoutRejectedException();
        if (Unavailable) throw new HttpRequestException("Commerce connection failed.");
        return Task.FromResult(new HttpResponseMessage(Status)
        {
            Content = Malformed ? new StringContent("invalid JSON") : Status != HttpStatusCode.OK ? JsonContent.Create(new { code = "PERMISSION_DENIED" })
                : JsonContent.Create(new
                {
                    data = new[] { new { courseId = Guid.CreateVersion7(), title = "Course without an offer" } },
                    pagination = new { page = 2, size = 10, total = 11, totalPages = 2 }
                }),
        });
    }
}
