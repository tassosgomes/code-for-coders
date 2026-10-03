using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.Commerce.IntegrationTests;

internal static class AccessExpirationContract
{
    public static void AssertValid(JsonElement payload)
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "courtesy.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        var document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
        var schema = document["components"]!["schemas"]!["AcessoExpiradoPayload"]!.DeepClone();
        schema["properties"]!["origin"] = document["components"]!["schemas"]!["OrigemDaConcessao"]!.DeepClone();
        var result = JsonSchema.FromText(schema.ToJsonString()).Evaluate(payload, new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(result.IsValid, "Expiration fact does not match AsyncAPI AcessoExpiradoPayload.");
    }
}
