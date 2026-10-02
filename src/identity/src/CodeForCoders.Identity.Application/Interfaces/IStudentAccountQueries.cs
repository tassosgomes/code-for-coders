namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStudentAccountQueries
{
    Task<StudentAccountDetails?> FindAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken);
}
