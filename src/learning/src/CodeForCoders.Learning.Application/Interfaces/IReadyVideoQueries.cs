namespace CodeForCoders.Learning.Application.Interfaces;

public interface IReadyVideoQueries
{
    Task<bool> IsReadyAsync(Guid videoId, CancellationToken cancellationToken);
}
