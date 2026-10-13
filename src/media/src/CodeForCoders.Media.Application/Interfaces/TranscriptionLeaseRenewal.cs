namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionLeaseRenewal(Guid TranscriptionId, Guid LeaseId, DateTimeOffset Now, DateTimeOffset LeaseUntil);
