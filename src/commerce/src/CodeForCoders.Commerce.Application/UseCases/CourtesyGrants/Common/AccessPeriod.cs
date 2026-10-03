using System.Text.Json.Serialization;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AccessPeriod(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Months)
{
    [JsonIgnore]
    public bool MonthsSpecified { get; init; }
}
