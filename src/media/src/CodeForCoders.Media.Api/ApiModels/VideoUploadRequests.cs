namespace CodeForCoders.Media.Api.ApiModels;

public sealed record CreateVideoUploadRequest(
    string Title,
    string FileName,
    long FileSize,
    string ContentType,
    string Fingerprint,
    string UploaderName,
    IReadOnlyList<string>? Tags = null);

public sealed record CreateVideoUploadPartUrlsRequest(IReadOnlyList<int> PartNumbers);
