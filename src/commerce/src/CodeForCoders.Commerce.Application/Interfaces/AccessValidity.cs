using System.Text.Json.Serialization;
namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record AccessValidity(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? EndsOn);
