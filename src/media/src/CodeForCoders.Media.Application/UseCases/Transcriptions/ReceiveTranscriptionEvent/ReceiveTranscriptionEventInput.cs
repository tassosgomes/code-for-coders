namespace CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;

public sealed record ReceiveTranscriptionEventInput(byte[] Body, string EventId, string Timestamp, string Signature)
{
    public const int MaximumBodyBytes = 16 * 1024;
}
