using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Learning.IntegrationTests;

/// <summary>Messages learning sends, checked against contracts/learning/asyncapi.yaml.</summary>
internal static class LearningMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("learning/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
