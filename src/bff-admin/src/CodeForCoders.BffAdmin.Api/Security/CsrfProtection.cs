using System.Security.Cryptography;
using System.Text;

namespace CodeForCoders.BffAdmin.Api.Security;

public static class CsrfProtection
{
    public static bool IsValid(string? cookieValue, string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var cookieBytes = Encoding.UTF8.GetBytes(cookieValue);
        var headerBytes = Encoding.UTF8.GetBytes(headerValue);
        return CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}
