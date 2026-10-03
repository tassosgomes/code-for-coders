using System.Text.Json.Serialization;

namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentAccessPeriod(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Months);
