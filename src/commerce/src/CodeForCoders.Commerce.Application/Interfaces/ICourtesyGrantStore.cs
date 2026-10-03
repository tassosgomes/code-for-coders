using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICourtesyGrantStore
{
    Task<GrantReceipt?> FindReceiptAsync(GrantScope scope, CancellationToken cancellationToken);
    Task<string?> FindCourseTitleAsync(Guid courseId, CancellationToken cancellationToken);
    Task<IGrantTransaction> LockAsync(GrantScope scope, CancellationToken cancellationToken);
    Task<Enrollment> GetOrCreateEnrollmentAsync(Guid studentId, Guid courseId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessGrant?> GetAsync(Guid grantId, CancellationToken cancellationToken);
    void Add(AccessGrant grant);
    void Add(GrantReceipt receipt);
}
