namespace CodeForCoders.Media.Api.ApiModels;

public sealed record CreateVideoUploadRequest(
    string Title,
    string FileName,
    long FileSize,
    string ContentType,
    string Fingerprint,
    string UploaderName);

public sealed record CreateVideoUploadPartUrlsRequest(IReadOnlyList<int> PartNumbers);
