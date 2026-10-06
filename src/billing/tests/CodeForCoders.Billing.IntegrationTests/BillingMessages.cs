using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Billing.IntegrationTests;

/// <summary>Messages billing sends, checked against contracts/billing/asyncapi.yaml.</summary>
internal static class BillingMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("billing/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
