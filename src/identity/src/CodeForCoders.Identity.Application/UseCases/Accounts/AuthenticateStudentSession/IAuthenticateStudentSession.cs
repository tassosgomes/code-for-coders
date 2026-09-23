using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;

public interface IAuthenticateStudentSession : IUseCase<AuthenticateStudentSessionInput, AuthenticateStudentSessionOutput>;
