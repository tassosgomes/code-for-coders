using System.Net;
using System.Text;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class StudentLessonBoundaryHandler : HttpMessageHandler
{
    public int StatusCode { get; set; } = 200;
    public string Body { get; set; } = "{\"lesson\":{\"title\":\"Lesson title\"},\"course\":{\"title\":\"Course title\"}}";
    public string? Token { get; private set; }
    public string? Path { get; private set; }
    public bool Timeout { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Path = request.RequestUri?.AbsolutePath;
        Token = request.Headers.Authorization?.Parameter;
        if (Timeout) throw new TaskCanceledException("Controlled timeout");
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)StatusCode) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
    }
}
