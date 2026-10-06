using CodeForCoders.Identity.Application.Interfaces;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.GetStudentContact;

public interface IGetStudentContact : IUseCase<GetStudentContactInput, StudentContact>;
