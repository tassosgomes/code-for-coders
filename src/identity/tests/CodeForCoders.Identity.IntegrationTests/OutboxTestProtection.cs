using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.IntegrationTests;

internal static class OutboxTestProtection
{
    public static OutboxPayloadProtector Protector { get; } = new(Options.Create(new OutboxProtectionOptions
    {
        KeyBase64 = Convert.ToBase64String(Enumerable.Range(100, 32).Select(value => (byte)value).ToArray()),
    }));

    public static string ReadPayload(OutboxMessage message) => Protector.Unprotect(message);
}
