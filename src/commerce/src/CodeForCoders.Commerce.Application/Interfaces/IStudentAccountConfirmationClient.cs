namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IStudentAccountConfirmationClient
{
    Task<bool?> ConfirmAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken);
}
