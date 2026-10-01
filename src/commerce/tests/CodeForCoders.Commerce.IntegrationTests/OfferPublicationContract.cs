using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.Commerce.IntegrationTests;

internal static class OfferPublicationContract
{
    public static void AssertValid(JsonElement payload, bool audit = false, bool changed = false)
    {
        var document = Read(audit ? "audit.yaml" : "offers.yaml");
        if (audit) Assert.Equal("1.3.0", document["info"]!["version"]!.GetValue<string>());
        var name = audit ? "AtoPraticadoPayload" : changed ? "OfertaAlteradaPayload" : "OfertaPayload";
        var schema = Inline(document["components"]!["schemas"]![name]!, document);
        var result = JsonSchema.FromText(schema.ToJsonString()).Evaluate(payload, new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(result.IsValid, $"Outbox/broker payload does not match AsyncAPI {name}.");
    }

    private static JsonNode Read(string name)
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", name));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        return JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
    }

    private static JsonNode Inline(JsonNode node, JsonNode document)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is { } reference)
            {
                var parts = reference.GetValue<string>().Split('#');
                var source = parts[0].Length == 0 ? document : Read(parts[0]);
                var target = source;
                foreach (var segment in parts[1].Split('/').Skip(1)) target = target[segment]!;
                return Inline(target, source);
            }
            var result = new JsonObject();
            foreach (var (key, value) in obj) result[key] = value is null ? null : Inline(value, document);
            return result;
        }
        if (node is JsonArray array) return new JsonArray(array.Select(value => value is null ? null : Inline(value, document)).ToArray());
        return node.DeepClone();
    }
}
