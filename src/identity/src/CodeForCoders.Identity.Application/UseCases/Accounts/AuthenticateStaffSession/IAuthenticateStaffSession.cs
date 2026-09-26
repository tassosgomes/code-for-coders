using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;

public interface IAuthenticateStaffSession : IUseCase<AuthenticateStaffSessionInput, AuthenticateStaffSessionOutput>;
