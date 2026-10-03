using System.Text.Json.Nodes;
using System.Text.Json;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.Commerce.IntegrationTests;

internal static class AccessDecisionContract
{
    public static void AssertValid(string payload)
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "access-decision.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        var document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
        var schema = document["components"]!["schemas"]!["AccessDecision"]!;
        using var response = JsonDocument.Parse(payload);
        var result = JsonSchema.FromText(schema.ToJsonString()).Evaluate(response.RootElement,
            new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(result.IsValid, "HTTP access decision does not match the approved OpenAPI schema.");
    }
}
