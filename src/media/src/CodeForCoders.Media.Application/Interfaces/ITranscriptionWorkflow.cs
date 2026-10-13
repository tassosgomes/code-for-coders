namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionWorkflow
{
    Task<bool> ExecuteNextAsync(TranscriptionCycle cycle, CancellationToken cancellationToken);
    Task MaintainAsync(string workDirectory, CancellationToken cancellationToken);
}
