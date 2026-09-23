using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;

public interface IValidateStudentSession : IUseCase<ValidateStudentSessionInput, ValidateStudentSessionOutput?>;
