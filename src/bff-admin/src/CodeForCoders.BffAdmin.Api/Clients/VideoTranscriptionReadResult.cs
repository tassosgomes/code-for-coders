using System.Net;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record VideoTranscriptionReadResult(
    HttpStatusCode StatusCode, string? Code, byte[]? Body, string? ContentType, string? CacheControl);
