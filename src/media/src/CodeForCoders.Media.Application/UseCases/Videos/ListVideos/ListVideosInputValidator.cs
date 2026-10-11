using FluentValidation;

namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideos;

public sealed class ListVideosInputValidator : AbstractValidator<ListVideosInput>
{
    public ListVideosInputValidator()
    {
        RuleFor(input => input.Tags).Must(tags => tags is null || tags.Count <= 10);
        RuleFor(input => input.Tags).Must(tags => tags is null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 100));
        RuleFor(input => input.TagMatch).Must(value => value is "all" or "any");
    }
}
