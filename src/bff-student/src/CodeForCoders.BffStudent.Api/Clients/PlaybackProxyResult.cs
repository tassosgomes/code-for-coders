namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record PlaybackProxyResult(int StatusCode, byte[]? Body = null, string? ContentType = null, string? Code = null, string? Reason = null, DateTimeOffset? AccessEndedAt = null);
