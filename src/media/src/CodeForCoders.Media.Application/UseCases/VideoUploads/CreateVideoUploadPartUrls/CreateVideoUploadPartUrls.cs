using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUploadPartUrls;

public sealed class CreateVideoUploadPartUrls(
    ITenantContext tenantContext,
    IVideoUploadRepository videoUploads,
    IMediaStoragePort mediaStorage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICreateVideoUploadPartUrls
{
    public async Task<VideoPartUrlsOutput> ExecuteAsync(
        CreateVideoUploadPartUrlsInput input,
        CancellationToken cancellationToken)
    {
        var (_, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        var upload = await videoUploads.GetOwnedAsync(input.UploadId, actorAccountId, false, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (upload is null || upload.ExpiresAt <= now)
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }

        if (input.PartNumbers.Count is < 1 or > 100
            || input.PartNumbers.Distinct().Count() != input.PartNumbers.Count)
        {
            throw new MediaApiException(400, "INVALID_REQUEST", "The requested part list is invalid.");
        }

        if (input.PartNumbers.Any(partNumber => partNumber < 1 || partNumber > upload.PartCount))
        {
            throw new MediaApiException(422, "PART_OUT_OF_RANGE", "A part number is outside this upload.");
        }

        var expiresAt = now.AddMinutes(60);
        var parts = new List<VideoPartUrlOutput>(input.PartNumbers.Count);
        foreach (var partNumber in input.PartNumbers)
        {
            var url = await mediaStorage.CreatePartUploadUriAsync(
                upload.ObjectKey,
                upload.StorageUploadId,
                partNumber,
                expiresAt,
                cancellationToken);
            parts.Add(new VideoPartUrlOutput(partNumber, url, expiresAt));
        }

        upload.ExtendExpiry(expiresAt);
        await unitOfWork.CommitAsync(cancellationToken);
        return new VideoPartUrlsOutput(parts, upload.ExpiresAt);
    }
}
