using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateLesson;

public sealed record CreateLessonInput(CourseWriteContext Context, Guid ModuleId, CourseChanges Changes);
