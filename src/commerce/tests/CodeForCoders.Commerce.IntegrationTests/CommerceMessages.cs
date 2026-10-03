using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Commerce.IntegrationTests;

/// <summary>Messages commerce sends, checked against contracts/commerce/asyncapi.yaml.</summary>
internal static class CommerceMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("commerce/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
