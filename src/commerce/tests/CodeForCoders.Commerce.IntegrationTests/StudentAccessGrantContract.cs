using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;

namespace CodeForCoders.Commerce.IntegrationTests;

internal static class StudentAccessGrantContract
{
    public static bool IsValid(JsonNode payload)
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "access-decision.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        var document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
        var schema = Inline(document["components"]!["schemas"]!["AccessGrantPage"]!, document);
        using var response = JsonDocument.Parse(payload.ToJsonString());
        return JsonSchema.FromText(schema.ToJsonString()).Evaluate(response.RootElement,
            new EvaluationOptions { RequireFormatValidation = true }).IsValid;
    }

    private static JsonNode Inline(JsonNode node, JsonNode document)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is { } reference)
            {
                var target = document;
                foreach (var segment in reference.GetValue<string>().Split('/').Skip(1)) target = target[segment]!;
                return Inline(target, document);
            }
            var result = new JsonObject();
            foreach (var (key, value) in obj) result[key] = value is null ? null : Inline(value, document);
            return result;
        }
        if (node is JsonArray array) return new JsonArray(array.Select(value => value is null ? null : Inline(value, document)).ToArray());
        return node.DeepClone();
    }
}
