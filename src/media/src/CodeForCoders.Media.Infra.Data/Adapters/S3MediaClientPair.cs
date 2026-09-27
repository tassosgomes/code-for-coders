using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class S3MediaClientPair : IDisposable
{
    public S3MediaClientPair(IOptions<AwsMediaOptions> options)
    {
        var settings = options.Value;
        var credentials = string.IsNullOrWhiteSpace(settings.AccessKeyId)
            ? DefaultAWSCredentialsIdentityResolver.GetCredentials(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region),
            })
            : new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey);

        Internal = CreateClient(settings.EndpointInternal, settings, credentials);
        Public = string.Equals(settings.EndpointInternal, settings.EndpointPublic, StringComparison.Ordinal)
            ? Internal
            : CreateClient(settings.EndpointPublic, settings, credentials);
    }

    public IAmazonS3 Internal { get; }

    public IAmazonS3 Public { get; }

    public void Dispose()
    {
        Internal.Dispose();
        if (!ReferenceEquals(Public, Internal))
        {
            Public.Dispose();
        }
    }

    private static IAmazonS3 CreateClient(
        string? serviceUrl,
        AwsMediaOptions options,
        AWSCredentials credentials)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
            UseHttp = serviceUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ?? false,
        };

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
        }

        return new AmazonS3Client(credentials, config);
    }
}
