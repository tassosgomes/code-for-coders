using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.ListPendingVideoUploads;

public interface IListPendingVideoUploads : IUseCase<ListPendingVideoUploadsInput, VideoUploadPageOutput>;

public sealed record ListPendingVideoUploadsInput(int Page, int Size);
