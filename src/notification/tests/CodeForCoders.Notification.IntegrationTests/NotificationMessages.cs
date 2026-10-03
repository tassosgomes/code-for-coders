using System.Text.Json;
using CodeForCoders.ContractTesting;

namespace CodeForCoders.Notification.IntegrationTests;

/// <summary>Messages notification sends, checked against contracts/notification/asyncapi.yaml.</summary>
internal static class NotificationMessages
{
    private static readonly AsyncApiContract Contract = AsyncApiContract.Load("notification/asyncapi.yaml");

    public static void AssertSends(string routingKey, JsonElement payload) => Contract.AssertSends(routingKey, payload);

    public static void AssertSends(string routingKey, string payloadJson) => Contract.AssertSends(routingKey, payloadJson);
}
