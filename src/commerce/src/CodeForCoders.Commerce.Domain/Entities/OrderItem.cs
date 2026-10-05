namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record OrderItem(Guid CourseId, string CourseTitle, Guid OfferId, string OfferName, int PriceCents, string PeriodType, int? PeriodMonths);
