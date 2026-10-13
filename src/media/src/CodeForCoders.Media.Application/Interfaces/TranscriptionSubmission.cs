namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionSubmission(Guid TranscriptionId, int SubmissionNumber, Uri SourceUrl);
