using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;

public interface IValidateStaffSession : IUseCase<ValidateStaffSessionInput, ValidateStaffSessionOutput?>;
