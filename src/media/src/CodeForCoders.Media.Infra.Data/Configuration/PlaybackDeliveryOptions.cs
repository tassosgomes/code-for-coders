namespace CodeForCoders.Media.Infra.Data.Configuration;

public sealed class PlaybackDeliveryOptions
{
    public const string SectionName = "Playback:Delivery";
    public string Adapter { get; set; } = "DevelopmentEdge";
    public string BaseAddress { get; set; } = "http://localhost:5109/";
    public string SharedSecret { get; set; } = string.Empty;
    public string KeyPairId { get; set; } = string.Empty;
    public string PrivateKeyBase64 { get; set; } = string.Empty;
    public bool HasValidCredentials()
    {
        if (Adapter == "DevelopmentEdge") return SharedSecret.Length >= 16;
        if (string.IsNullOrWhiteSpace(KeyPairId)) return false;
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(PrivateKeyBase64), out _);
            return rsa.KeySize >= 2048;
        }
        catch (FormatException) { return false; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

}
