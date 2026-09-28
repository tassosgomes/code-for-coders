using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Infra.Data.Outbox;

public sealed class OutboxPayloadProtector
{
    public const string Algorithm = "A256GCM";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] key;
    private readonly byte[] fingerprintKey;

    public OutboxPayloadProtector(IOptions<OutboxProtectionOptions> options)
    {
        var settings = options.Value;
        key = Convert.FromBase64String(settings.KeyBase64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("BFF outbox protection key must contain exactly 256 bits.");
        }

        KeyVersion = settings.KeyVersion;
        fingerprintKey = HMACSHA256.HashData(key, "BffAdmin/AuditComplementIdempotency/v1"u8);
    }

    public string KeyVersion { get; }

    public byte[] ComputeFingerprint(Guid recordId, string explanation)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(
            new FingerprintInput(recordId, explanation),
            JsonOptions);
        return HMACSHA256.HashData(fingerprintKey, request);
    }

    public string Protect(Guid messageId, Guid tenantId, string payload)
    {
        var plaintext = Encoding.UTF8.GetBytes(payload);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(messageId, tenantId));

        return JsonSerializer.Serialize(
            new ProtectedPayload(
                Algorithm,
                KeyVersion,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String([.. ciphertext, .. tag])),
            JsonOptions);
    }

    public string Unprotect(OutboxMessage message)
    {
        return Unprotect(message.Payload, message.Id, message.TenantId, message.PayloadKeyVersion);
    }

    public string Unprotect(string payload, Guid messageId, Guid tenantId, string? payloadKeyVersion)
    {
        var envelope = ReadEnvelope(payload);
        if (envelope is null)
        {
            return payload;
        }

        if (!string.Equals(envelope.KeyVersion, KeyVersion, StringComparison.Ordinal)
            || !string.Equals(payloadKeyVersion, KeyVersion, StringComparison.Ordinal))
        {
            throw new CryptographicException("Protected outbox payload uses an unsupported key version.");
        }

        var nonce = Convert.FromBase64String(envelope.Nonce);
        var sealedData = Convert.FromBase64String(envelope.Ciphertext);
        if (nonce.Length != NonceSize || sealedData.Length < TagSize)
        {
            throw new CryptographicException("Protected outbox payload is malformed.");
        }

        var ciphertext = sealedData.AsSpan(0, sealedData.Length - TagSize);
        var tag = sealedData.AsSpan(sealedData.Length - TagSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData(messageId, tenantId));
        return Encoding.UTF8.GetString(plaintext);
    }

    private static ProtectedPayload? ReadEnvelope(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("$protected", out var algorithm))
        {
            return null;
        }

        if (algorithm.GetString() != Algorithm)
        {
            throw new CryptographicException("Protected outbox payload uses an unsupported algorithm.");
        }

        return document.RootElement.Deserialize<ProtectedPayload>(JsonOptions)
            ?? throw new CryptographicException("Protected outbox payload is malformed.");
    }

    private static byte[] AssociatedData(Guid messageId, Guid tenantId)
        => Encoding.UTF8.GetBytes($"{messageId:D}|{tenantId:D}");

    private sealed record FingerprintInput(Guid RecordId, string Explanation);

    private sealed record ProtectedPayload(
        [property: JsonPropertyName("$protected")] string Algorithm,
        string KeyVersion,
        string Nonce,
        string Ciphertext);
}
