using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;

namespace CodeForCoders.Commerce.Api.ApiModels;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AccessPeriodRequest(string Type, JsonElement Months)
{
    public AccessPeriod ToInput()
        => new(Type, Months.ValueKind == JsonValueKind.Number && Months.TryGetInt32(out var months) ? months : null)
        {
            // Presence matters for lifetime: even an explicit null months member is forbidden.
            MonthsSpecified = Months.ValueKind != JsonValueKind.Undefined,
        };
}
