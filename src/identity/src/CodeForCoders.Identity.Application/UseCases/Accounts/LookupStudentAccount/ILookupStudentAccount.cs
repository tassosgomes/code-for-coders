using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStudentAccount;

public interface ILookupStudentAccount : IUseCase<LookupStudentAccountInput, StudentAccountDetails>;
