namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class Enrollment
{
    private Enrollment() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public DateTimeOffset FirstGrantedAt { get; private set; }
    public static Enrollment Create(Guid tenantId, Guid studentId, Guid courseId, DateTimeOffset now)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, StudentId = studentId, CourseId = courseId, FirstGrantedAt = now };
}
