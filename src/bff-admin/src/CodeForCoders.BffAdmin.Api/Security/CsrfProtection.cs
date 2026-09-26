using System.Security.Cryptography;
using System.Text;

namespace CodeForCoders.BffAdmin.Api.Security;

public static class CsrfProtection
{
    public static bool IsValid(string? expectedValue, string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(expectedValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedValue);
        var headerBytes = Encoding.UTF8.GetBytes(headerValue);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, headerBytes);
    }
}
