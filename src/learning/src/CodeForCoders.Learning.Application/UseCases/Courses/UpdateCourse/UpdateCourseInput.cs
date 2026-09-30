using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateCourse;

public sealed record UpdateCourseInput(CourseWriteContext Context, CourseChanges Changes);
