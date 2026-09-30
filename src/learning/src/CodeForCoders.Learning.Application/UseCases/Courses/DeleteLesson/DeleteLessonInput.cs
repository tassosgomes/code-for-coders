using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DeleteLesson;

public sealed record DeleteLessonInput(CourseWriteContext Context, Guid LessonId);
