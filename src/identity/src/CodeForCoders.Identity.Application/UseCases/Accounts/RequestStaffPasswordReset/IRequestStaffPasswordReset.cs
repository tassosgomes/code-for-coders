using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStaffPasswordReset;

public interface IRequestStaffPasswordReset
    : IUseCase<RequestStaffPasswordResetInput, RequestStaffPasswordResetOutput>;
