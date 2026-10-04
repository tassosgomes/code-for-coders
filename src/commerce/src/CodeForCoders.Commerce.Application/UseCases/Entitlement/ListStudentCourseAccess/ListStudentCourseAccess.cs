using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.ListStudentCourseAccess;

public sealed class ListStudentCourseAccess(IStudentCourseAccessQueries queries, TimeProvider clock,
    IValidator<ListStudentCourseAccessInput> validator) : IListStudentCourseAccess
{
    public async Task<StudentCourseAccessList> ExecuteAsync(ListStudentCourseAccessInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return new(await queries.ListAsync(new(input.StudentId, clock.GetUtcNow()), cancellationToken));
    }
}
