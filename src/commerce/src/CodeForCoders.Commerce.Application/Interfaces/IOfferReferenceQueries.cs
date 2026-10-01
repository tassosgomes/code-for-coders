namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IOfferReferenceQueries
{
    Task<OfferReferenceList> ResolveAsync(IReadOnlyCollection<Guid> offerIds, CancellationToken cancellationToken);
}

public sealed record OfferReference(Guid OfferId, string Label);
public sealed record OfferReferenceList(IReadOnlyCollection<OfferReference> Data);
