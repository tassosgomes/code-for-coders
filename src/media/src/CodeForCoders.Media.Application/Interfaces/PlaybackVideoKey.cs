namespace CodeForCoders.Media.Application.Interfaces;

public sealed record PlaybackVideoKey(Guid VideoId, string MasterKeyId, byte[] Ciphertext);
