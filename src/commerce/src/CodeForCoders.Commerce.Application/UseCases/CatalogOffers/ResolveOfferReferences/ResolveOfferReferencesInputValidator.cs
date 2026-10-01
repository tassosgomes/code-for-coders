using System.Text.Json;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ResolveOfferReferences;

public sealed class ResolveOfferReferencesInputValidator : AbstractValidator<ResolveOfferReferencesInput>
{
    public ResolveOfferReferencesInputValidator()
    {
        RuleFor(input => input.Body).Must(IsValid).WithErrorCode("INVALID_REQUEST")
            .WithMessage("offerIds must contain between 1 and 50 distinct UUIDs; no other properties are allowed.");
    }

    private static bool IsValid(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Count() != 1
            || !body.TryGetProperty("offerIds", out var ids) || ids.ValueKind != JsonValueKind.Array
            || ids.GetArrayLength() is < 1 or > 50) return false;
        var distinct = new HashSet<Guid>();
        return ids.EnumerateArray().All(id => id.ValueKind == JsonValueKind.String
            && id.TryGetGuid(out var value) && value != Guid.Empty && distinct.Add(value));
    }
}
