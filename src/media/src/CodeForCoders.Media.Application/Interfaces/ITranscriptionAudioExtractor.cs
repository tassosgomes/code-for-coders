namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionAudioExtractor
{
    Task ExtractAndUploadAsync(TranscriptionWorkItem work, string workDirectory, CancellationToken cancellationToken);
}
