using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.GetVideoUpload;

public interface IGetVideoUpload : IUseCase<GetVideoUploadInput, VideoUploadOutput>;

public sealed record GetVideoUploadInput(Guid UploadId);
