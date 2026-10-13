namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionQueries
{
    Task<TranscriptionContentSnapshot?> GetCurrentAsync(Guid videoId, CancellationToken cancellationToken);
}
