namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.PreviewCourtesyTerm;

public sealed record CourtesyTermPreview(int Months, DateTimeOffset ComputedAt, DateOnly EndsOn, DateTimeOffset ExpiresAt);
