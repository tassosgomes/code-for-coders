using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GrantCourtesy;

public sealed record GrantCourtesyInput(Guid TenantId, Guid ActorId, Guid StudentId, Guid CourseId,
    AccessPeriod? AccessPeriod, string? Reason, string IdempotencyKey, string? TraceParent);
