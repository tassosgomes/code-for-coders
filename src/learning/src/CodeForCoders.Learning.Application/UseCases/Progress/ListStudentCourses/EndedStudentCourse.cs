namespace CodeForCoders.Learning.Application.UseCases.Progress.ListStudentCourses;

public sealed record EndedStudentCourse(Guid CourseId, string Title, DateOnly EndedOn, string EndedReason, StudentCourseProgressSummary? Progress);
