using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateLesson;

public sealed record UpdateLessonInput(CourseWriteContext Context, Guid LessonId, CourseChanges Changes);
