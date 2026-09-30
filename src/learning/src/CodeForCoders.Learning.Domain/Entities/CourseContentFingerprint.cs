using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodeForCoders.Learning.Domain.Entities;

internal static class CourseContentFingerprint
{
    public static string FromCourse(Course course) => Hash(course.Title, course.Description, course.Level, course.PrerequisiteText, course.RecommendedCourseIds,
        course.Modules.OrderBy(module => module.Position).Select(module =>
            new PublishedModule(module.Id, module.Title, module.Position,
                module.Lessons.OrderBy(lesson => lesson.Position).Select(lesson =>
                    new PublishedLesson(lesson.Id, lesson.Title, lesson.Description, lesson.Position, lesson.VideoId ?? Guid.Empty)).ToArray())).ToArray());

    public static string FromVersion(CourseVersion version) => Hash(version.Title, version.Description, null, null, [], version.Modules);

    private static string Hash(string title, string? description, string? level, string? prerequisiteText,
        IReadOnlyList<Guid> recommendedCourseIds, IReadOnlyList<PublishedModule> modules)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { title, description, level, prerequisiteText, recommendedCourseIds, modules }))));
}
