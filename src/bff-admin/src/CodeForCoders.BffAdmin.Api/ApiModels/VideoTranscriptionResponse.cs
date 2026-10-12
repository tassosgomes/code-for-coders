namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record VideoTranscriptionResponse(string Status, bool Readable, DateTimeOffset? ReadyAt, string? FailureReason, bool CanStart);
