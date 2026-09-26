using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;

public interface IRevokeStaffSession : IUseCase<RevokeStaffSessionInput, bool>;
