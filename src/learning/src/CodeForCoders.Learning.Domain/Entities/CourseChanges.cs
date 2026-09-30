namespace CodeForCoders.Learning.Domain.Entities;

public sealed record CourseChanges(string? Title, string? Description, bool HasDescription, int? Position, Guid? ModuleId,
    Guid? VideoId = null, bool HasVideoId = false, string? Level = null, bool HasLevel = false,
    string? PrerequisiteText = null, bool HasPrerequisiteText = false, IReadOnlyList<Guid>? RecommendedCourseIds = null);
