using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.GetVideoUpload;

public sealed class GetVideoUpload(
    ITenantContext tenantContext,
    IVideoUploadRepository videoUploads,
    IMediaStoragePort mediaStorage,
    TimeProvider timeProvider) : IGetVideoUpload
{
    public async Task<VideoUploadOutput> ExecuteAsync(
        GetVideoUploadInput input,
        CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        var upload = await videoUploads.GetOwnedAsync(input.UploadId, tenantId, actorAccountId, false, cancellationToken);
        if (upload is null || upload.ExpiresAt <= timeProvider.GetUtcNow())
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }

        try
        {
            var parts = await mediaStorage.ListPartsAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
            return VideoUploadUseCaseHelpers.ToOutput(upload, parts.Select(part => part.PartNumber).ToArray());
        }
        catch (MultipartUploadNotFoundException)
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }
    }
}
