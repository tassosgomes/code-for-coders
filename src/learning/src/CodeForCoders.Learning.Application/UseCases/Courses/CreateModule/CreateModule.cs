using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateModule;

public sealed class CreateModule(CourseEditSession session) : ICreateModule
{
    public Task<CourseEditOutput> ExecuteAsync(CreateModuleInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"create-module", course => { return course.AddModule(input.Changes); }, cancellationToken);
}
