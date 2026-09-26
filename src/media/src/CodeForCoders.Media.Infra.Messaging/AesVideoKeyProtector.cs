using System.Security.Cryptography;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class AesVideoKeyProtector : IVideoKeyProtector, IDisposable
{
    private const byte FormatVersion = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int HeaderLength = 1 + NonceLength + TagLength;
    private readonly byte[] masterKey;
    private readonly string masterKeyId;

    public AesVideoKeyProtector(IOptions<VideoPreparationOptions> options)
    {
        var settings = options.Value;
        masterKey = Convert.FromBase64String(settings.MasterKey);
        masterKeyId = settings.MasterKeyId;
    }

    public ProtectedVideoKey Protect(Guid videoId, byte[] videoKey)
    {
        var result = new byte[HeaderLength + videoKey.Length];
        result[0] = FormatVersion;
        var nonce = result.AsSpan(1, NonceLength);
        var tag = result.AsSpan(1 + NonceLength, TagLength);
        var ciphertext = result.AsSpan(HeaderLength);
        RandomNumberGenerator.Fill(nonce);
        using var cipher = new AesGcm(masterKey, TagLength);
        cipher.Encrypt(nonce, videoKey, ciphertext, tag, videoId.ToByteArray());
        return new ProtectedVideoKey(result, masterKeyId);
    }

    public byte[] Unprotect(Guid videoId, string keyId, byte[] encryptedVideoKey)
    {
        if (!string.Equals(keyId, masterKeyId, StringComparison.Ordinal)
            || encryptedVideoKey.Length <= HeaderLength
            || encryptedVideoKey[0] != FormatVersion)
        {
            throw new CryptographicException("The encrypted video key cannot be opened with the configured master key.");
        }

        var videoKey = new byte[encryptedVideoKey.Length - HeaderLength];
        var nonce = encryptedVideoKey.AsSpan(1, NonceLength);
        var tag = encryptedVideoKey.AsSpan(1 + NonceLength, TagLength);
        var ciphertext = encryptedVideoKey.AsSpan(HeaderLength);
        using var cipher = new AesGcm(masterKey, TagLength);
        cipher.Decrypt(nonce, ciphertext, tag, videoKey, videoId.ToByteArray());
        return videoKey;
    }

    public void Dispose()
        => CryptographicOperations.ZeroMemory(masterKey);
}
