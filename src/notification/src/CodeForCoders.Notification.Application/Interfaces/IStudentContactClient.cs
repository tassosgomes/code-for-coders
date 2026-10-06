namespace CodeForCoders.Notification.Application.Interfaces;

public interface IStudentContactClient
{
    Task<StudentContact?> GetAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken);
}
