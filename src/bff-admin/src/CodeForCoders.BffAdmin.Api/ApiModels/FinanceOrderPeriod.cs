using System.Text.Json.Serialization;
namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record FinanceOrderPeriod(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Months);
