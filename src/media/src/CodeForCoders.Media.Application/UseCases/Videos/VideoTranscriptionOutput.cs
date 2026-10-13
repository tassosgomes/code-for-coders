using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.Videos;

public sealed record VideoTranscriptionOutput(string Status, bool Readable, DateTimeOffset? ReadyAt, string? FailureReason, bool CanStart)
{
    public static VideoTranscriptionOutput FromState(TranscriptionState state)
        => new(state.Status, state.Readable, state.ReadyAt, state.FailureReason, state.CanStart);
}
