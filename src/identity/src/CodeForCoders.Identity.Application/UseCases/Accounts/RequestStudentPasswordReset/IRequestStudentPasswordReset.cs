using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;

public interface IRequestStudentPasswordReset
    : IUseCase<RequestStudentPasswordResetInput, RequestStudentPasswordResetOutput>
{
}
