using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseLearningHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Token { get; private set; }
    public string? ActorName { get; private set; }
    public string? Key { get; private set; }
    public Uri? Uri { get; private set; }
    public CourseCreateBody? Body { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public bool Malformed { get; set; }
    public Guid CourseId { get; } = Guid.CreateVersion7();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Token = request.Headers.Authorization?.Parameter; Uri = request.RequestUri;
        ActorName = request.Headers.TryGetValues("X-Actor-Name", out var names) ? names.Single() : null;
        Key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
        if (request.Content is not null) Body = await request.Content.ReadFromJsonAsync<CourseCreateBody>(cancellationToken);
        if (Malformed) return new(HttpStatusCode.OK) { Content = new StringContent("invalid json") };
        if (Status != HttpStatusCode.OK) return new(Status) { Content = JsonContent.Create(new { code = "COURSE_NOT_FOUND" }) };
        var actor = new CourseActor("Validated teacher");
        var now = DateTimeOffset.UtcNow;
        var detail = new CourseDetail(CourseId, Body?.Title ?? "School course", Body?.Description, "draft", null, false, 1, now, actor, now, actor, []);
        if (request.Method == HttpMethod.Post) return new(HttpStatusCode.Created) { Content = JsonContent.Create(detail) };
        if (request.RequestUri!.Query.Length > 0)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CoursePage([new(CourseId, "School course", "draft", null, false, now, actor)], new(1, 20, 1, 1))) };
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(detail) };
    }
}
