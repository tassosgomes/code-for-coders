using FluentValidation;

namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideoTags;

public sealed class ListVideoTagsInputValidator : AbstractValidator<ListVideoTagsInput>
{
    public ListVideoTagsInputValidator()
    {
        RuleFor(input => input.Query)
            .Must(query => query is null || (!string.IsNullOrWhiteSpace(query) && query.Length <= 100));
    }
}
