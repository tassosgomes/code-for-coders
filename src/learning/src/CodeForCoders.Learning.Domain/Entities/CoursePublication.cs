namespace CodeForCoders.Learning.Domain.Entities;

public sealed record CoursePublication(int DraftRevision, string? VersionNote, CourseCreation Actor,
    IReadOnlyList<PublishedRecommendedCourse>? RecommendedCourses = null);
