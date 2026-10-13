using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Application.Common;

public sealed class TranscriptionWebhookSignature(IOptions<TranscriptionWebhookOptions> options, TimeProvider clock)
{
    private const int TimestampToleranceSeconds = 300;

    public bool IsValid(ReceiveTranscriptionEventInput input)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (string.IsNullOrWhiteSpace(input.EventId) || input.EventId.Length > 128
            || input.Timestamp.Length < 10 || !input.Timestamp.All(char.IsAsciiDigit)
            || !long.TryParse(input.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || timestamp < now - TimestampToleranceSeconds || timestamp > now + TimestampToleranceSeconds)
            return false;

        var prefix = Encoding.UTF8.GetBytes($"{input.EventId}.{input.Timestamp}.");
        var message = new byte[prefix.Length + input.Body.Length];
        prefix.CopyTo(message, 0);
        input.Body.CopyTo(message, prefix.Length);
        var valid = false;
        foreach (var secret in options.Value.Secrets.Where(secret => !string.IsNullOrWhiteSpace(secret)))
        {
            var expected = HMACSHA256.HashData(Convert.FromBase64String(secret[6..]), message);
            foreach (var signature in input.Signature.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!signature.StartsWith("v1,", StringComparison.Ordinal)) continue;
                var decoded = new byte[expected.Length];
                if (Convert.TryFromBase64String(signature[3..], decoded, out var length) && length == expected.Length)
                    valid |= CryptographicOperations.FixedTimeEquals(expected, decoded);
            }
        }
        return valid;
    }
}
