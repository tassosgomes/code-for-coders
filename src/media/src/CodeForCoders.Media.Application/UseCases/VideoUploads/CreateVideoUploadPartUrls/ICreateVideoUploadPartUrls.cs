using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUploadPartUrls;

public interface ICreateVideoUploadPartUrls : IUseCase<CreateVideoUploadPartUrlsInput, VideoPartUrlsOutput>;

public sealed record CreateVideoUploadPartUrlsInput(Guid UploadId, IReadOnlyList<int> PartNumbers);
