using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateModule;

public sealed class UpdateModule(CourseEditSession session) : IUpdateModule
{
    public Task<CourseEditOutput> ExecuteAsync(UpdateModuleInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"update-module:{input.ModuleId:D}", course => { course.UpdateModule(input.ModuleId, input.Changes); return null; }, cancellationToken);
}
