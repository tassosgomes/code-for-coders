using System.Security.Cryptography;

namespace CodeForCoders.Commerce.Api.Clients;

public sealed class StudentAccountIdentityOptions
{
    public string BaseAddress { get; set; } = "http://identity:8080/";
    public string Issuer { get; set; } = "commerce";
    public string Audience { get; set; } = "identity-internal";
    public string SigningKeyId { get; set; } = "local-commerce-identity-1";
    public string SigningKeyBase64 { get; set; } = "";
    public string SchoolTimeZone { get; set; } = "America/Sao_Paulo";
    public static bool IsValid(StudentAccountIdentityOptions options)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.SigningKeyBase64), out _);
            _ = TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone);
            return rsa.KeySize >= 2048 && options.Issuer == "commerce" && options.Audience == "identity-internal"
                && !string.IsNullOrWhiteSpace(options.SigningKeyId)
                && Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
