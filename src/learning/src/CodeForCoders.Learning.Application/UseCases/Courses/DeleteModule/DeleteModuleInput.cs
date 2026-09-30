using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DeleteModule;

public sealed record DeleteModuleInput(CourseWriteContext Context, Guid ModuleId);
