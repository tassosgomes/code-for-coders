using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStudentAccountIdentityClient
{
    Task<StudentAccountResolutionResult> ResolveAsync(Guid staffSessionId, IReadOnlyList<Guid> studentIds, CancellationToken cancellationToken);
    Task<StudentAccountLookupResult> LookupAsync(Guid staffSessionId, StudentAccountLookupRequestV1 input, CancellationToken cancellationToken);
}
