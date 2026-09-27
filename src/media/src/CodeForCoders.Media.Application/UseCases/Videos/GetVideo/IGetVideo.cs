using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideo;

public interface IGetVideo : IUseCase<GetVideoInput, VideoOutput?>;
