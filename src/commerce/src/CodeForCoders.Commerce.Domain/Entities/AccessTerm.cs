namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record AccessTerm(DateOnly? EndsOn, DateTimeOffset? ExpiresAt)
{
    public static AccessTerm Calculate(DateTimeOffset grantedAt, string type, int? months, TimeZoneInfo zone)
    {
        if (type == "lifetime" && months is null) return new(null, null);
        if (type != "months" || months is null or < 1 or > 60)
            throw new EntitlementRuleException("FIELD_INVALID", "Access period is invalid.");
        var endsOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(grantedAt, zone).DateTime).AddMonths(months.Value);
        var midnight = endsOn.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // The first existing local instant starts the next day; overlap uses its earliest UTC occurrence.
        while (zone.IsInvalidTime(midnight)) midnight = midnight.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(midnight) ? zone.GetAmbiguousTimeOffsets(midnight).Max() : zone.GetUtcOffset(midnight);
        return new(endsOn, new DateTimeOffset(midnight, offset).ToUniversalTime());
    }
}
