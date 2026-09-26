namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record CreateVideoUploadRequest(
    string Title,
    string FileName,
    long FileSize,
    string ContentType,
    string Fingerprint);

public sealed record CreateVideoUploadInternalRequest(
    string Title,
    string FileName,
    long FileSize,
    string ContentType,
    string Fingerprint,
    string UploaderName);

public sealed record CreateVideoUploadPartUrlsRequest(IReadOnlyList<int> PartNumbers);

public sealed record VideoUploadResponse(
    Guid UploadId,
    string Title,
    string FileName,
    long FileSize,
    long PartSize,
    int PartCount,
    IReadOnlyList<int> ReceivedParts,
    DateTimeOffset ExpiresAt);

public sealed record VideoUploadPaginationResponse(int Page, int Size, long Total, long TotalPages);

public sealed record VideoUploadPageResponse(
    IReadOnlyList<VideoUploadResponse> Data,
    VideoUploadPaginationResponse Pagination);

public sealed record VideoPartUrlResponse(int PartNumber, Uri Url, DateTimeOffset ExpiresAt);

public sealed record VideoPartUrlsResponse(
    IReadOnlyList<VideoPartUrlResponse> Parts,
    DateTimeOffset UploadExpiresAt);
