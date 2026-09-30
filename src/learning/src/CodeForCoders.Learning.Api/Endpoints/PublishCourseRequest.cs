namespace CodeForCoders.Learning.Api.Endpoints;

public sealed record PublishCourseRequest(int DraftRevision, string? VersionNote);
