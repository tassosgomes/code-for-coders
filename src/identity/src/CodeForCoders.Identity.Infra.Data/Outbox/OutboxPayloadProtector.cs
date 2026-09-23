using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Infra.Data.Outbox;

/// <summary>
/// Encrypts outbox payloads that carry addressed data (recipient and tokenized links) so the
/// persisted JSONB never holds them in clear text. The ciphertext is bound to the message id and
/// tenant, and only the publisher decrypts it right before sending it to the broker.
/// </summary>
public sealed class OutboxPayloadProtector
{
    public const string Algorithm = "A256GCM";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] key;

    public OutboxPayloadProtector(IOptions<OutboxProtectionOptions> options)
    {
        key = Convert.FromBase64String(options.Value.KeyBase64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Outbox protection key must contain exactly 256 bits.");
        }
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
                Convert.ToBase64String(nonce),
                Convert.ToBase64String([.. ciphertext, .. tag])),
            JsonOptions);
    }

    public string Unprotect(OutboxMessage message)
    {
        var envelope = ReadEnvelope(message.Payload);
        if (envelope is null)
        {
            return message.Payload;
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
        aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData(message.Id, message.TenantId));
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

    private sealed record ProtectedPayload(
        [property: JsonPropertyName("$protected")] string Algorithm,
        string Nonce,
        string Ciphertext);
}
