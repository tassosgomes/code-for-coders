using CodeForCoders.BffAdmin.Api.ApiModels;
namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record CommerceFinanceOrderSummary(Guid OrderId, string Number, Guid StudentId, string Status, Guid CourseId, string CourseTitle, string OfferName, int PriceCents, string Currency, string? PaymentMethod, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);
