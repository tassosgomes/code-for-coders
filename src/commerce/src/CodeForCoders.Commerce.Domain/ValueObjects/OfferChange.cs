namespace CodeForCoders.Commerce.Domain.ValueObjects;

public sealed record OfferChange(string? Name, decimal? PriceCents, AccessPeriod? AccessPeriod);
