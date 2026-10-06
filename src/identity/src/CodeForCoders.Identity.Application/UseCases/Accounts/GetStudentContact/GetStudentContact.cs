using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.GetStudentContact;

public sealed class GetStudentContact(IStudentAccountQueries queries) : IGetStudentContact
{
    public async Task<StudentContact> ExecuteAsync(GetStudentContactInput input, CancellationToken cancellationToken)
        => await queries.FindContactAsync(input.TenantId, input.StudentId, cancellationToken)
            ?? throw new StudentContactNotFoundException();
}
