using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionWorkItem(Transcription Attempt, Video Video)
{
    public bool WasSignaled { get; init; }
    public string AudioObjectKey => $"{Attempt.TenantId:D}/{Attempt.VideoId:D}/transcription/{Attempt.TranscriptionId:D}/audio.m4a";
    public string LocalDirectory(string root) => Path.Combine(root, "transcription", Attempt.TranscriptionId.ToString("N"), Attempt.LeaseId!.Value.ToString("N"));
}
