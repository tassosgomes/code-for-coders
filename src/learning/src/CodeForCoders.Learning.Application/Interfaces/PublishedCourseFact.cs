using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.Interfaces;

public static class PublishedCourseFact
{
    public const string Route = "conteudo.versao-publicada.v1";

    public static OutboxMessageDraft FromVersion(CourseVersion version, string? traceParent)
        => new(version.Id, version.TenantId, Route, Route, new
        {
            EventId = version.Id,
            version.TenantId,
            version.CourseId,
            version.VersionNumber,
            version.PublishedAt,
            version.PublishedById,
            version.Title,
            Description = version.Description ?? string.Empty,
            version.Level,
            Prerequisite = version.Prerequisite ?? new PublishedPrerequisite(null, []),
            Modules = version.Modules.Select(module => new
            {
                module.ModuleId,
                module.Title,
                module.Position,
                Lessons = module.Lessons.Select(lesson => new { lesson.LessonId, lesson.Title, lesson.Position, lesson.VideoId })
            }),
        }, version.PublishedAt, traceParent);
}
