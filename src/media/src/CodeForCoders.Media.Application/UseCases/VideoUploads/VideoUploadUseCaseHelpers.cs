using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Domain.Entities;
using System.Security.Cryptography;
using System.Text.Json;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads;

internal static class VideoUploadUseCaseHelpers
{
    public static (Guid TenantId, Guid ActorAccountId) RequireActor(ITenantContext context)
    {
        if (context.TenantId is not Guid tenantId || context.ActorAccountId is not Guid actorAccountId)
        {
            throw new MediaApiException(
                401,
                "TOKEN_INVALID",
                "The actor token is invalid.");
        }

        return (tenantId, actorAccountId);
    }

    public static MediaApiException UploadNotFound()
        => new(404, "UPLOAD_NOT_FOUND", "The video upload was not found.");

    public static VideoUploadOutput ToOutput(
        VideoUpload upload,
        IReadOnlyList<int> receivedParts)
        => VideoUploadOutput.From(upload, receivedParts);

    public static string HashRequest<TRequest>(TRequest request)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    public static TResponse Replay<TResponse>(OperationIdempotencyRecord record)
        => JsonSerializer.Deserialize<TResponse>(record.ResponseJson)
            ?? throw new InvalidOperationException("The stored idempotency response is invalid.");

    public static void EnsureSameRequest(OperationIdempotencyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new MediaApiException(422, "IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used with another request.");
        }
    }

    public static OperationIdempotencyRecord CreateOrReuseIdempotencyRecord(
        OperationIdempotencyRecord? existing,
        Guid tenantId,
        Guid actorAccountId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset now)
    {
        if (existing is not null)
        {
            existing.Reuse(requestHash, now.AddHours(24));
            return existing;
        }

        var created = OperationIdempotencyRecord.Create(
            tenantId,
            actorAccountId,
            operation,
            key,
            requestHash,
            now.AddHours(24));
        return created;
    }
}
