using System.Text.Json;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.UpdateCatalogCourse;

public sealed class UpdateCatalogCourseInputValidator : AbstractValidator<UpdateCatalogCourseInput>
{
    public UpdateCatalogCourseInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(input => input.Body).Must(body => body.ValueKind == JsonValueKind.Object
            && body.EnumerateObject().All(property => property.Name == "tagline"
                && property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null)
            && body.EnumerateObject().Select(property => property.Name).Distinct().Count() == body.EnumerateObject().Count());
    }
}
