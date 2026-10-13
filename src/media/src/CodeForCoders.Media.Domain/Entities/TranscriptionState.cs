namespace CodeForCoders.Media.Domain.Entities;

public sealed record TranscriptionState(string Status, bool Readable, DateTimeOffset? ReadyAt, string? FailureReason, bool CanStart)
{
    public static TranscriptionState Derive(string videoStatus, IReadOnlyList<Transcription> attempts, bool enabled)
    {
        if (!enabled || videoStatus != "ready") return new("not-requested", false, null, null, false);
        var current = attempts.SingleOrDefault(attempt => attempt.IsCurrent);
        var latest = attempts.OrderByDescending(attempt => attempt.RequestedAt).ThenByDescending(attempt => attempt.TranscriptionId).FirstOrDefault();
        var ongoing = attempts.Any(attempt => attempt.Status is "queued" or "working" or "waiting");
        var failed = latest?.Status == "failed";
        return new(ongoing ? "in-progress" : failed ? "failed" : current is not null ? "ready" : "not-requested",
            current is not null, current?.ReadyAt, !ongoing && failed ? latest?.FailureReason : null,
            !ongoing && !(failed && latest?.FailureReason is "duration-limit" or "size-limit"));
    }
}
