namespace CodeForCoders.BffAdmin.IntegrationTests;

internal static class SharedOutboxTestProtection
{
    internal const string KeyVersion = "bff-admin-integration-test-v1";

    internal static string KeyBase64 { get; } = Convert.ToBase64String(
        Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
}
