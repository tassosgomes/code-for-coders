using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Interfaces;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 310_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
