using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

/// <summary>
/// Anti-corruption adapter from the media port to a CloudFront distribution.
/// Signing policy can evolve here without leaking CloudFront details to Application.
/// </summary>
public sealed class CloudFrontMediaCdnAdapter(IOptions<AwsMediaOptions> options) : IMediaCdnPort
{
    public Uri CreateDeliveryUri(string objectKey)
    {
        var domain = options.Value.CloudFrontDistributionDomain.TrimEnd('/');
        var path = string.Join(
            "/",
            objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return new Uri($"https://{domain}/{path}", UriKind.Absolute);
    }
}
