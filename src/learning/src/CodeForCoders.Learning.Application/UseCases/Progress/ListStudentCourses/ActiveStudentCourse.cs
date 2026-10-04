namespace CodeForCoders.Learning.Application.UseCases.Progress.ListStudentCourses;

public sealed record ActiveStudentCourse(Guid CourseId, string Title, bool? Started, DateTimeOffset? LastActivityAt, Guid ContinueLessonId, StudentCourseProgressSummary? Progress);
