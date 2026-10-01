using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ResolveOfferReferences;

public sealed class ResolveOfferReferences(IOfferReferenceQueries queries, IValidator<ResolveOfferReferencesInput> validator)
    : IResolveOfferReferences
{
    public async Task<OfferReferenceList> ExecuteAsync(ResolveOfferReferencesInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var ids = input.Body.GetProperty("offerIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
        return await queries.ResolveAsync(ids, cancellationToken);
    }
}
