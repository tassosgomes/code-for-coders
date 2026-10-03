using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Identity.IntegrationTests;

/// <summary>Messages identity sends, checked against contracts/identity/asyncapi.yaml.</summary>
internal static class IdentityMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("identity/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
