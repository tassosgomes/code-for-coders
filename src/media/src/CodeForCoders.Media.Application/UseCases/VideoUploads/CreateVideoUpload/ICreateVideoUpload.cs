using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUpload;

public interface ICreateVideoUpload : IUseCase<CreateVideoUploadInput, CreateVideoUploadOutput>;

public sealed record CreateVideoUploadInput(
    string Title,
    string FileName,
    long FileSize,
    string ContentType,
    string Fingerprint,
    string UploaderName,
    string IdempotencyKey);

public sealed record CreateVideoUploadOutput(VideoUploadOutput Upload, bool Resumed = false);
