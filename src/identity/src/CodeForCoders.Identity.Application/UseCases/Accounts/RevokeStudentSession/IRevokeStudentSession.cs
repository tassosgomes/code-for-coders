using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;

public interface IRevokeStudentSession : IUseCase<RevokeStudentSessionInput, bool>;
