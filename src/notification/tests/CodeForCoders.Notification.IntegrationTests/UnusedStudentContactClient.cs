using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.IntegrationTests;

internal sealed class UnusedStudentContactClient : IStudentContactClient
{
    public Task<StudentContact?> GetAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken)
        => throw new NotSupportedException("This fixture exercises direct-email notifications only.");
}
