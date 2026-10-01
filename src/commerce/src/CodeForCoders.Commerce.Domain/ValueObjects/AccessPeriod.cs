using System.Text.Json.Serialization;
using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Domain.ValueObjects;

public sealed record AccessPeriod(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Months)
{
    public static AccessPeriod Create(string type, decimal? months)
    {
        if (type == "lifetime" && months is null) return new(type, null);
        if (type == "months" && months is >= 1 and <= 60 && decimal.Truncate(months.Value) == months)
            return new(type, (int)months.Value);
        throw new CatalogRuleException("FIELD_INVALID", "accessPeriod must be lifetime or between 1 and 60 whole months.");
    }
}
