namespace CodeForCoders.Learning.Domain.Entities;

public sealed class CourseCreationReceipt
{
    private CourseCreationReceipt() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }

    public static CourseCreationReceipt Create(Guid tenantId, Guid actorId, string key)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = actorId, Key = key };

    public void Store(string hash, string responseJson, DateTimeOffset now)
    {
        RequestHash = hash; ResponseJson = responseJson; ExpiresAt = now.AddHours(24);
    }
}
