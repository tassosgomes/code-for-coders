using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStudentAccountIdentityClient
{
    Task<StudentAccountLookupResult> LookupAsync(Guid staffSessionId, StudentAccountLookupRequestV1 input, CancellationToken cancellationToken);
}
