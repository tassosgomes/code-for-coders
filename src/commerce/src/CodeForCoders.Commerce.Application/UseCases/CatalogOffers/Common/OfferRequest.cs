using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;

public static class OfferRequest
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Canonical(JsonElement body)
        => body.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", body.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
            JsonValueKind.Number when body.TryGetDecimal(out var number) => number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            _ => body.GetRawText()
        };

    public static bool HasValidShape(JsonElement body, bool creation)
    {
        if (body.ValueKind != JsonValueKind.Object) return false;
        var properties = body.EnumerateObject().ToArray();
        if (properties.Length == 0 || properties.Select(property => property.Name).Distinct().Count() != properties.Length) return false;
        if (creation && (properties.Length != 3 || !body.TryGetProperty("name", out _)
            || !body.TryGetProperty("priceCents", out _) || !body.TryGetProperty("accessPeriod", out _))) return false;
        return properties.All(property => property.Name switch
        {
            "name" => property.Value.ValueKind == JsonValueKind.String,
            "priceCents" => property.Value.ValueKind == JsonValueKind.Number,
            "accessPeriod" => ValidPeriodShape(property.Value),
            _ => false
        });
    }

    private static bool ValidPeriodShape(JsonElement period)
        => period.ValueKind == JsonValueKind.Object && period.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && period.EnumerateObject().Select(property => property.Name).Distinct().Count() == period.EnumerateObject().Count()
            && period.EnumerateObject().All(property => property.Name == "type"
                || property.Name == "months" && property.Value.ValueKind == JsonValueKind.Number);

    public static OfferChange ParseChange(JsonElement body)
    {
        AccessPeriod? period = null;
        if (body.TryGetProperty("accessPeriod", out var value))
            period = AccessPeriod.Create(value.GetProperty("type").GetString()!,
                value.TryGetProperty("months", out var months) ? Decimal(months) : null);
        return new(body.TryGetProperty("name", out var name) ? name.GetString() : null,
            body.TryGetProperty("priceCents", out var price) ? Decimal(price) : null, period);
    }

    private static decimal Decimal(JsonElement value) => value.TryGetDecimal(out var number) ? number : decimal.MaxValue;
}
