using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseLearningHandler : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }
    public JsonElement? Payload { get; private set; }
    public JsonElement? Pendencies { get; set; }
    public bool ReferencesUnavailable { get; set; }
    public string ProblemCode { get; set; } = "COURSE_NOT_FOUND";
    public string? Location { get; set; }
    public IReadOnlyList<JsonElement> Modules { get; set; } = [];

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
        Calls++; Method = request.Method; Token = request.Headers.Authorization?.Parameter; Uri = request.RequestUri;
        ActorName = request.Headers.TryGetValues("X-Actor-Name", out var names) ? names.Single() : null;
        Key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
        if (request.Content is not null)
        {
            Payload = await request.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Body = request.RequestUri!.AbsolutePath == "/internal/v1/courses" && Payload.Value.TryGetProperty("title", out _) ? Payload.Value.Deserialize<CourseCreateBody>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
        }
        if (Malformed) return new(HttpStatusCode.OK) { Content = new StringContent("invalid json") };
        if (Status != HttpStatusCode.OK) return new(Status) { Content = JsonContent.Create(new { code = ProblemCode, pendencies = Pendencies, errors = new { title = new[] { "Title is required." } } }) };
        if (request.RequestUri!.AbsolutePath == "/internal/v1/course-references/resolve")
            return new(ReferencesUnavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new[] { new { courseId = CourseId, title = "School course" } } }) };
        var actor = new CourseActor("Validated teacher");
        var now = DateTimeOffset.UtcNow;
        var detail = new CourseDetail(CourseId, Body?.Title ?? "School course", Body?.Description, "draft", null, false, 1, now, actor, now, actor, Modules);
        if (request.RequestUri!.AbsolutePath.Contains("/versions/", StringComparison.Ordinal))
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CourseVersion(CourseId, 1, "Historical title", "Historical description", now, actor, "First note", false, [])) };
        if (request.RequestUri!.AbsolutePath.EndsWith("/discard-draft", StringComparison.Ordinal))
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(detail with { Status = "published", CurrentVersion = 2, DraftRevision = 5 }) };
        if (request.RequestUri!.AbsolutePath.EndsWith("/versions", StringComparison.Ordinal) && request.Method == HttpMethod.Get)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CourseVersionSummaryPage([new(2, now, actor, "Second note", true), new(1, now.AddDays(-1), actor, "First note", false)], new(1, 20, 2, 1))) };
        if (request.RequestUri!.AbsolutePath.EndsWith("/versions", StringComparison.Ordinal))
            return new(HttpStatusCode.Created) { Content = JsonContent.Create(new CourseVersion(CourseId, 1, "School course", null, now, actor, "Note", true, [])), Headers = { Location = new Uri($"/internal/v1/courses/{CourseId}/versions/1", UriKind.Relative) } };
        if (request.Method == HttpMethod.Post)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(detail) };
            if (Location is not null) response.Headers.Location = new Uri(Location, UriKind.Relative);
            return response;
        }
        if (request.RequestUri!.Query.Length > 0)
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CoursePage([new(CourseId, "School course", "draft", null, false, now, actor)], new(1, 20, 1, 1))) };
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(detail) };
    }
}
