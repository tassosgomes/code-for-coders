namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PurchaseAccessFact(Guid EventId, Guid TenantId, Guid GrantId, Guid StudentId, Guid CourseId,
 string Origin, Guid? OriginRef, DateTimeOffset GrantedAt);
