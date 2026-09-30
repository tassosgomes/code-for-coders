using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateModule;

public sealed record UpdateModuleInput(CourseWriteContext Context, Guid ModuleId, CourseChanges Changes);
