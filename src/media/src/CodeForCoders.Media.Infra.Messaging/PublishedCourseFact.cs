using System.Text.Json;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed record PublishedCourseFact(Guid EventId, Guid TenantId, Guid CourseId, int VersionNumber,
    DateTimeOffset PublishedAt, IReadOnlyList<(Guid LessonId, Guid VideoId)> References)
{
    public const string Route = "conteudo.versao-publicada.v1";

    public static PublishedCourseFact Parse(ReadOnlyMemory<byte> body, string route, string? messageId)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        try
        {
            var eventId = root.GetProperty("eventId").GetGuid();
            var tenantId = root.GetProperty("tenantId").GetGuid();
            var courseId = root.GetProperty("courseId").GetGuid();
            var version = root.GetProperty("versionNumber").GetInt32();
            var publishedAt = root.GetProperty("publishedAt").GetDateTimeOffset();
            if (route != Route || eventId == Guid.Empty || tenantId == Guid.Empty || courseId == Guid.Empty
                || version < 1 || messageId != eventId.ToString()) throw new JsonException("Invalid publication identity.");
            var modules = root.GetProperty("modules").EnumerateArray().ToArray();
            if (modules.Length is < 1 or > 100) throw new JsonException("Invalid publication curriculum.");
            var references = new List<(Guid, Guid)>();
            foreach (var module in modules)
            {
                var lessons = module.GetProperty("lessons").EnumerateArray().ToArray();
                if (lessons.Length is < 1 or > 200) throw new JsonException("Invalid publication curriculum.");
                foreach (var lesson in lessons)
                {
                    var lessonId = lesson.GetProperty("lessonId").GetGuid();
                    var videoId = lesson.GetProperty("videoId").GetGuid();
                    if (lessonId == Guid.Empty || videoId == Guid.Empty) throw new JsonException("Invalid publication reference.");
                    references.Add((lessonId, videoId));
                }
            }
            if (references.Select(reference => reference.Item1).Distinct().Count() != references.Count)
                throw new JsonException("Duplicate publication reference.");
            return new(eventId, tenantId, courseId, version, publishedAt, references);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new JsonException("Invalid course publication payload.");
        }
    }
}
