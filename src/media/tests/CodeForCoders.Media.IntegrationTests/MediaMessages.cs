using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Media.IntegrationTests;

/// <summary>Messages media sends, checked against contracts/media/asyncapi.yaml.</summary>
internal static class MediaMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("media/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
