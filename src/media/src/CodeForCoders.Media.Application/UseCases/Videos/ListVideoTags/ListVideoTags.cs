using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideoTags;

public sealed class ListVideoTags(IVideoQueries videoQueries, IValidator<ListVideoTagsInput> validator) : IListVideoTags
{
    public async Task<VideoTagListOutput> ExecuteAsync(ListVideoTagsInput input, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
            throw new MediaApiException(400, "INVALID_REQUEST", "The tag query is invalid.");
        return new VideoTagListOutput(await videoQueries.ListTagsAsync(input.Query, cancellationToken));
    }
}
