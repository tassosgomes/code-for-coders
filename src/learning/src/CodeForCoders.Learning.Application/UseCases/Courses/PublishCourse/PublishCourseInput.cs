using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.PublishCourse;

public sealed record PublishCourseInput(CourseWriteContext Context, int DraftRevision, string? VersionNote);
