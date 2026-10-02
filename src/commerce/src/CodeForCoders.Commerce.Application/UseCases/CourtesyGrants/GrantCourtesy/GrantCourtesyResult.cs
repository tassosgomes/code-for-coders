using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GrantCourtesy;

public sealed record GrantCourtesyResult(CourtesyGrant Grant, bool Replayed);
