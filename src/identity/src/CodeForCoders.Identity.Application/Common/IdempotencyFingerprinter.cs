using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Common;

public sealed class IdempotencyFingerprinter : IIdempotencyFingerprinter
{
    private readonly byte[] key;

    public IdempotencyFingerprinter(IOptions<IdempotencyOptions> options)
    {
        key = Convert.FromBase64String(options.Value.FingerprintKeyBase64);
    }

    public string HashKey(string keyValue) => Hash($"key:{keyValue}");

    public string Fingerprint(string name, string email, string password)
        => FingerprintValues(name, email, password);

    public string Fingerprint(string value)
        => FingerprintValues(value);

    private string FingerprintValues(params string[] values)
    {
        var serialized = JsonSerializer.Serialize(values);
        return Hash($"fingerprint:{serialized}");
    }

    private string Hash(string value)
        => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
}
