using CodeForCoders.Media.Domain.ValueObjects;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class VideoTag
{
    private VideoTag() { }
    public Guid TagId { get; private set; }
    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static VideoTag Create(Guid tenantId, VideoTagValue value, DateTimeOffset now)
        => new()
        {
            TagId = Guid.CreateVersion7(),
            TenantId = tenantId,
            DisplayName = value.DisplayName,
            NormalizedName = value.NormalizedName,
            CreatedAt = now
        };
}
