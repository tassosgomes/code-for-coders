namespace CodeForCoders.Media.Application.Interfaces;

public interface IExpirePendingVideoUploads
{
    Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken);
}
