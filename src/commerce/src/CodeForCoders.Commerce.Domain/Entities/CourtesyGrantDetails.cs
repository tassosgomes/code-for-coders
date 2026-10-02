namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record CourtesyGrantDetails(Guid ActorId, string Reason, string PeriodType, int? Months, DateTimeOffset Now);
