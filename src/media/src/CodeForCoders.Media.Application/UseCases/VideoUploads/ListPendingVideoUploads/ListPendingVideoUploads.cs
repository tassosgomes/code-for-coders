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
        var pending = new List<VideoUploadOutput>(input.Size);
        var now = timeProvider.GetUtcNow();
        long visibleTotal = 0;
        long candidateTotal;
        var candidatePage = 1;
        const int scanSize = 50;
        do
        {
            var (uploads, total) = await videoUploads.ListPendingAsync(
                tenantId,
                actorAccountId,
                now,
                candidatePage++,
                scanSize,
                cancellationToken);
            candidateTotal = total;
            foreach (var upload in uploads)
            {
                try
                {
                    var parts = await mediaStorage.ListPartsAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
                    if (visibleTotal >= (long)(input.Page - 1) * input.Size && pending.Count < input.Size)
                    {
                        pending.Add(VideoUploadUseCaseHelpers.ToOutput(
                            upload,
                            parts.Select(part => part.PartNumber).ToArray()));
                    }

                    visibleTotal++;
                }
                catch (MultipartUploadNotFoundException)
                {
                    // An externally removed multipart upload cannot be resumed and is reclaimed by the expiration worker.
                }
            }
        }
        while ((long)(candidatePage - 1) * scanSize < candidateTotal);

        return new VideoUploadPageOutput(
            pending,
            new VideoUploadPaginationOutput(
                input.Page,
                input.Size,
                visibleTotal,
                (long)Math.Ceiling(visibleTotal / (double)input.Size)));
    }
}
