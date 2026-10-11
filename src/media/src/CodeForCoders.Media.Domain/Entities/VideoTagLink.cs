namespace CodeForCoders.Media.Domain.Entities;

public sealed class VideoTagLink
{
    private VideoTagLink() { }
    public Guid VideoId { get; private set; }
    public Guid TagId { get; private set; }
    public Guid TenantId { get; private set; }

    public static VideoTagLink Create(Guid videoId, VideoTag tag)
        => new() { VideoId = videoId, TagId = tag.TagId, TenantId = tag.TenantId };
}
