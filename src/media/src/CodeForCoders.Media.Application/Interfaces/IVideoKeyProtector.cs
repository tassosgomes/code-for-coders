namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoKeyProtector
{
    ProtectedVideoKey Protect(Guid videoId, byte[] videoKey);

    byte[] Unprotect(Guid videoId, string masterKeyId, byte[] encryptedVideoKey);
}

public sealed record ProtectedVideoKey(byte[] Ciphertext, string MasterKeyId);
