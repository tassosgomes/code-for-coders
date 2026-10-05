using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;
namespace CodeForCoders.Commerce.IntegrationTests;

internal static class OrderHttpContract
{
    public static void AssertValid(JsonElement payload, string schemaName)
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "commerce", "openapi-internal.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        var document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
        JsonNode Resolve(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                if (obj["$ref"] is JsonValue reference)
                {
                    var target = document;
                    foreach (var segment in reference.GetValue<string>()[2..].Split('/')) target = target![segment]!;
                    return Resolve(target!);
                }
                var copy = new JsonObject(); foreach (var entry in obj) copy[entry.Key] = entry.Value is null ? null : Resolve(entry.Value); return copy;
            }
            if (node is JsonArray array) return new JsonArray(array.Select(item => item is null ? null : Resolve(item)).ToArray());
            return node.DeepClone();
        }
        var schema = Resolve(document["components"]!["schemas"]![schemaName]!);
        var result = JsonSchema.FromText(schema.ToJsonString()).Evaluate(payload, new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(result.IsValid, $"HTTP response does not match {schemaName} in the current Commerce contract.");
    }
}
