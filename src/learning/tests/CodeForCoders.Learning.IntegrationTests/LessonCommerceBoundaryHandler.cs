using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class LessonCommerceBoundaryHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Assertion { get; private set; }
    public string? Path { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public StudentAccessDecision Decision { get; set; } = new("allowed", new("lifetime", null), null, null, DateTimeOffset.UtcNow);
    public Guid? PermittedCourseId { get; set; }
    public bool Timeout { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Assertion = request.Headers.Authorization?.Parameter; Path = request.RequestUri!.PathAndQuery;
        if (Timeout) throw new TaskCanceledException("Controlled upstream timeout");
        return Task.FromResult(new HttpResponseMessage(Status)
        {
            Content = JsonContent.Create(PermittedCourseId.HasValue && !Path.Contains($"courseId={PermittedCourseId:D}", StringComparison.Ordinal)
            ? new StudentAccessDecision("denied", null, "no-grant", null, DateTimeOffset.UtcNow) : Decision)
        });
    }
}
