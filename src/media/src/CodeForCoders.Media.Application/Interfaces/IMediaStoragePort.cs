namespace CodeForCoders.Media.Application.Interfaces;

/// <summary>
/// Vendor-neutral port for media persistence. AWS SDK types stay behind the anti-corruption layer.
/// </summary>
public interface IMediaStoragePort
{
    Task<MediaObjectReceipt> StageAsync(
        MediaObjectRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Vendor-neutral port for producing a media delivery URL.
/// </summary>
public interface IMediaCdnPort
{
    Uri CreateDeliveryUri(string objectKey);
}

public sealed record MediaObjectRequest(
    string ObjectKey,
    string ContentType,
    long ContentLength);

public sealed record MediaObjectReceipt(
    string ObjectKey,
    string ProviderReference,
    Uri DeliveryUri);
