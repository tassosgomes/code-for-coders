using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DeleteModule;

public sealed class DeleteModule(CourseEditSession session) : IDeleteModule
{
    public Task<CourseEditOutput> ExecuteAsync(DeleteModuleInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"delete-module:{input.ModuleId:D}", course => { course.RemoveModule(input.ModuleId); return null; }, cancellationToken);
}
