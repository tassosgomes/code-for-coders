namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class AccessGrant
{
    private AccessGrant() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Origin { get; private set; } = "courtesy";
    public Guid? OriginRef { get; private set; }
    public string PeriodType { get; private set; } = "";
    public int? PeriodMonths { get; private set; }
    public string Status { get; private set; } = "active";
    public DateTimeOffset GrantedAt { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? Reason { get; private set; }
    public Guid? GrantedBy { get; private set; }
    public Guid? ExpiryEventId { get; private set; }
    public DateTimeOffset? ExpiryPublishedAt { get; private set; }

    public void MarkExpiryFact(Guid eventId, DateTimeOffset now)
    {
        if (eventId == Guid.Empty || ExpiresAt is null || ExpiresAt > now || ExpiryPublishedAt is not null)
            throw new EntitlementRuleException("EXPIRY_FACT_INVALID", "Only an expired grant without an expiry fact can be marked.");
        ExpiryEventId = eventId;
        ExpiryPublishedAt = now;
    }

    public static AccessGrant CreatePurchase(Enrollment enrollment, PurchaseGrantDetails details, TimeZoneInfo zone)
    {
        if (details.OrderId == Guid.Empty) throw new EntitlementRuleException("FIELD_INVALID", "Order is required.");
        var term = AccessTerm.Calculate(details.Now, details.PeriodType, details.Months, zone);
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = enrollment.TenantId,
            EnrollmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            Origin = "purchase",
            OriginRef = details.OrderId,
            PeriodType = details.PeriodType,
            PeriodMonths = details.Months,
            GrantedAt = details.Now,
            EndsOn = term.EndsOn,
            ExpiresAt = term.ExpiresAt
        };
    }

    public static AccessGrant CreateCourtesy(Enrollment enrollment, CourtesyGrantDetails details, TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(details.Reason) || details.Reason.Length > 500 || details.ActorId == Guid.Empty)
            throw new EntitlementRuleException("FIELD_INVALID", "Reason and author are required.");
        var term = AccessTerm.Calculate(details.Now, details.PeriodType, details.Months, zone);
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = enrollment.TenantId,
            EnrollmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            PeriodType = details.PeriodType,
            PeriodMonths = details.Months,
            GrantedAt = details.Now,
            EndsOn = term.EndsOn,
            ExpiresAt = term.ExpiresAt,
            Reason = details.Reason,
            GrantedBy = details.ActorId
        };
    }
}
