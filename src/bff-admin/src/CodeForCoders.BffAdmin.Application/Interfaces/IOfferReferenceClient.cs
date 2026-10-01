namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface IOfferReferenceClient
{
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(string accessToken, IReadOnlyCollection<Guid> offerIds,
        CancellationToken cancellationToken);
}
