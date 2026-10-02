using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.StudentAccessGrants.ListStudentAccessGrants;

public sealed class ListStudentAccessGrants(IStudentAccessGrantQueries queries, IValidator<ListStudentAccessGrantsInput> validator)
    : IListStudentAccessGrants
{
    public async Task<StudentAccessGrantPage> ExecuteAsync(ListStudentAccessGrantsInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return await queries.ListAsync(new(input.StudentId, input.Page, input.Size), cancellationToken);
    }
}
