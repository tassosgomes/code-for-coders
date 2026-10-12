namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionServiceClient
{
    Task<string> SubmitAsync(TranscriptionSubmission submission, CancellationToken cancellationToken);
    Task<string?> GetStatusAsync(string jobId, CancellationToken cancellationToken);
    Task<byte[]?> GetResultAsync(string jobId, CancellationToken cancellationToken);
}
