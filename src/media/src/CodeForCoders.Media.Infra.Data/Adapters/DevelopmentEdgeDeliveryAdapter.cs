using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class DevelopmentEdgeDeliveryAdapter(IOptions<PlaybackDeliveryOptions> options) : ISegmentDeliveryPort
{
    public SegmentDelivery CreateSegmentAccess(string videoPrefix, DateTimeOffset expiresAt)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SharedSecret))
            throw new InvalidOperationException("The development delivery secret is required.");
        var prefix = "/" + videoPrefix.Trim('/') + "/";
        var expiration = expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Nginx secure_link defines this development-only signature format (ADR-0014).
#pragma warning disable CA5351
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{expiration}{prefix} {settings.SharedSecret}"));
#pragma warning restore CA5351
        var signature = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new(new Uri(new Uri(settings.BaseAddress), videoPrefix), new($"st={signature}&e={expiration}", expiresAt));
    }
}
