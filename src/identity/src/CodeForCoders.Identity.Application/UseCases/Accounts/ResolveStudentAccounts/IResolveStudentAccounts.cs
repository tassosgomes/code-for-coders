using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases;
namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveStudentAccounts;

public interface IResolveStudentAccounts : IUseCase<ResolveStudentAccountsInput, StudentAccountResolutionList>;
