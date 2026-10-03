using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.Identity.IntegrationTests;

internal static class StudentAccountLookupContract
{
    private static readonly JsonNode Contract = LoadContract();
    private static readonly JsonSchema ProblemSchema = JsonSchema.FromText(Contract["components"]!["schemas"]!["Problem"]!.ToJsonString());

    public static void AssertNotFound(JsonNode payload)
    {
        Assert.True(IsValidProblem(payload), "HTTP student account lookup error does not match the approved OpenAPI schema.");
        var example = Contract["components"]!["responses"]!["StudentAccountNotFound"]!["content"]!["application/problem+json"]!["examples"]!["naoEncontrada"]!["value"]!;
        foreach (var property in new[] { "type", "title", "status", "code" })
        {
            Assert.True(JsonNode.DeepEquals(example[property], payload[property]), $"Lookup error {property} differs from the published example.");
        }

        Assert.False(string.IsNullOrWhiteSpace(payload["traceId"]!.GetValue<string>()));
    }

    public static bool IsValidProblem(JsonNode payload)
    {
        using var response = JsonDocument.Parse(payload.ToJsonString());
        return ProblemSchema.Evaluate(response.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid;
    }

    private static JsonNode LoadContract()
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "student-account-confirmation.yaml"));
        var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        return JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
    }
}
