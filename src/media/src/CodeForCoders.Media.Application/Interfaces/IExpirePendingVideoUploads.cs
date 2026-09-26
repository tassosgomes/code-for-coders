namespace CodeForCoders.Media.Application.Interfaces;

public interface IExpirePendingVideoUploads
{
    Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken);

    Task<bool> ExpireAsync(Guid uploadId, CancellationToken cancellationToken);
}
