using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CompleteVideoUpload;

public interface ICompleteVideoUpload : IUseCase<CompleteVideoUploadInput, CompletedVideoOutput>;

public sealed record CompleteVideoUploadInput(Guid UploadId, string IdempotencyKey);
