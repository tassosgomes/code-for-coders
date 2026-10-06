namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStudentAccountQueries
{
    Task<StudentContact?> FindContactAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken);
    Task<bool> IsEligibleAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken);
    Task<StudentAccountDetails?> FindAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken);
}
