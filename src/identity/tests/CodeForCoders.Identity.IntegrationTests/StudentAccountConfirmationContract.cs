using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.Identity.IntegrationTests;

internal static class StudentAccountConfirmationContract
{
    private static readonly JsonSchema Schema = LoadSchema();

    public static void AssertValid(JsonNode payload)
        => Assert.True(IsValid(payload), "HTTP student account confirmation does not match the approved OpenAPI schema.");

    public static bool IsValid(JsonNode payload)
    {
        using var response = JsonDocument.Parse(payload.ToJsonString());
        return Schema.Evaluate(response.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid;
    }

    private static JsonSchema LoadSchema()
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "student-account-confirmation.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        var document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
        return JsonSchema.FromText(document["components"]!["schemas"]!["StudentAccountConfirmation"]!.ToJsonString());
    }
}
