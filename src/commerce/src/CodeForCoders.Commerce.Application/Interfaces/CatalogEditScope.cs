namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogEditScope(Guid TenantId, Guid ActorId, Guid CourseId, string Key);
