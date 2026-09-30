using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateModule;

public sealed record CreateModuleInput(CourseWriteContext Context, CourseChanges Changes);
