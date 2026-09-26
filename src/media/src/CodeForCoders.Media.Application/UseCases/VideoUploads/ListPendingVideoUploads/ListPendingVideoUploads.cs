using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.ListPendingVideoUploads;

public sealed class ListPendingVideoUploads(
    ITenantContext tenantContext,
    IVideoUploadRepository videoUploads,
    IMediaStoragePort mediaStorage,
    TimeProvider timeProvider) : IListPendingVideoUploads
{
    public async Task<VideoUploadPageOutput> ExecuteAsync(
        ListPendingVideoUploadsInput input,
        CancellationToken cancellationToken)
    {
        if (input.Page < 1 || input.Size is < 1 or > 50)
        {
            throw new MediaApiException(400, "INVALID_REQUEST", "The requested page is invalid.");
        }

        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        var (uploads, total) = await videoUploads.ListPendingAsync(
            tenantId,
            actorAccountId,
            timeProvider.GetUtcNow(),
            input.Page,
            input.Size,
            cancellationToken);

        var pending = new List<VideoUploadOutput>(uploads.Count);
        foreach (var upload in uploads)
        {
            try
            {
                var parts = await mediaStorage.ListPartsAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
                pending.Add(VideoUploadUseCaseHelpers.ToOutput(
                    upload,
                    parts.Select(part => part.PartNumber).ToArray()));
            }
            catch (MultipartUploadNotFoundException)
            {
                // An externally removed multipart upload cannot be resumed and is reclaimed by the expiration worker.
            }
        }

        return new VideoUploadPageOutput(
            pending,
            new VideoUploadPaginationOutput(
                input.Page,
                input.Size,
                total,
                (long)Math.Ceiling(total / (double)input.Size)));
    }
}
