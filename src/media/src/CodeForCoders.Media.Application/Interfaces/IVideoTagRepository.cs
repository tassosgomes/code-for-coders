using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.ValueObjects;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoTagRepository
{
    Task<bool> WithLockedVideoAsync(VideoTagWriteScope scope, Func<Video, CancellationToken, Task> update, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ReplaceAsync(Guid videoId, IReadOnlyList<VideoTagValue> tags, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetAsync(IReadOnlyList<Guid> videoIds, CancellationToken cancellationToken);
}
