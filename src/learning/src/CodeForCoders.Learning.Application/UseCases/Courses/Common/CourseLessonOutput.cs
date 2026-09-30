using System.Text.Json.Serialization;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseLessonOutput(Guid LessonId, string Title, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description, int Position, Guid? VideoId);
