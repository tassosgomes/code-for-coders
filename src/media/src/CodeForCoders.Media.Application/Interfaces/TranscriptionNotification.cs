namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionNotification(string EventId, Guid? TranscriptionId, DateTimeOffset ReceivedAt);
