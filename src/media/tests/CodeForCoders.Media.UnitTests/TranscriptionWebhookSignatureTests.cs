using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class TranscriptionWebhookSignatureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] CurrentKey = Enumerable.Range(1, 24).Select(value => (byte)value).ToArray();
    private static readonly byte[] PreviousKey = Enumerable.Range(101, 24).Select(value => (byte)value).ToArray();
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"eventId":"evt_1","jobId":"trn_1","status":"completed"}""");

    [Fact(DisplayName = nameof(CurrentSecretSignatureIsValid))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void CurrentSecretSignatureIsValid()
        => Assert.True(Verifier(CurrentKey).IsValid(Input(Sign(CurrentKey))));

    [Fact(DisplayName = nameof(PreviousSecretSignatureIsValidDuringRotation))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void PreviousSecretSignatureIsValidDuringRotation()
        => Assert.True(Verifier(CurrentKey, PreviousKey).IsValid(Input(Sign(PreviousKey))));

    [Fact(DisplayName = nameof(AnyMatchingSignatureOfSeveralIsAccepted))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void AnyMatchingSignatureOfSeveralIsAccepted()
    {
        var header = $"{Sign(new byte[] { 9, 9, 9 })} {Sign(CurrentKey)}";
        Assert.True(Verifier(CurrentKey).IsValid(Input(header)));
    }

    [Fact(DisplayName = nameof(SignatureFromUnknownSecretIsRejected))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void SignatureFromUnknownSecretIsRejected()
        => Assert.False(Verifier(CurrentKey, PreviousKey).IsValid(Input(Sign(new byte[] { 7, 7, 7, 7 }))));

    [Fact(DisplayName = nameof(AlteredBodyIsRejected))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void AlteredBodyIsRejected()
    {
        var altered = Encoding.UTF8.GetBytes("""{"eventId":"evt_1","jobId":"trn_1","status":"failed"}""");
        Assert.False(Verifier(CurrentKey).IsValid(Input(Sign(CurrentKey)) with { Body = altered }));
    }

    [Theory(DisplayName = nameof(MissingOrMalformedSignatureIsRejected))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v2,AAAA")]
    [InlineData("v1,not-base64!!")]
    [InlineData("v1,AAAA")]
    public void MissingOrMalformedSignatureIsRejected(string header)
        => Assert.False(Verifier(CurrentKey).IsValid(Input(header)));

    [Theory(DisplayName = nameof(TimestampOutsideToleranceIsRejected))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    [InlineData(-301)]
    [InlineData(301)]
    public void TimestampOutsideToleranceIsRejected(int offsetSeconds)
    {
        var timestamp = (Now.ToUnixTimeSeconds() + offsetSeconds).ToString();
        Assert.False(Verifier(CurrentKey).IsValid(Input(Sign(CurrentKey, timestamp: timestamp)) with { Timestamp = timestamp }));
    }

    [Theory(DisplayName = nameof(TimestampInsideToleranceIsAccepted))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    [InlineData(-300)]
    [InlineData(0)]
    [InlineData(300)]
    public void TimestampInsideToleranceIsAccepted(int offsetSeconds)
    {
        var timestamp = (Now.ToUnixTimeSeconds() + offsetSeconds).ToString();
        Assert.True(Verifier(CurrentKey).IsValid(Input(Sign(CurrentKey, timestamp: timestamp)) with { Timestamp = timestamp }));
    }

    [Theory(DisplayName = nameof(NonNumericOrMissingTimestampAndEventIdAreRejected))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    [InlineData("", "evt_1")]
    [InlineData("abc", "evt_1")]
    [InlineData("-1791295200", "evt_1")]
    [InlineData("1791295200", "")]
    public void NonNumericOrMissingTimestampAndEventIdAreRejected(string timestamp, string eventId)
    {
        var input = Input(Sign(CurrentKey)) with { Timestamp = timestamp, EventId = eventId };
        Assert.False(Verifier(CurrentKey).IsValid(input));
    }

    [Fact(DisplayName = nameof(SignatureHeaderIsBoundToEventId))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void SignatureHeaderIsBoundToEventId()
        => Assert.False(Verifier(CurrentKey).IsValid(Input(Sign(CurrentKey)) with { EventId = "evt_other" }));

    [Fact(DisplayName = nameof(SecretValidationRequiresPrefixedBase64AndAtMostTwo))]
    [Trait("Application", "TranscriptionWebhookSignature - Unit")]
    public void SecretValidationRequiresPrefixedBase64AndAtMostTwo()
    {
        var valid = Secret(CurrentKey);
        Assert.True(new TranscriptionWebhookOptions { Secrets = [valid, Secret(PreviousKey)] }.HasValidSecrets());
        Assert.True(new TranscriptionWebhookOptions { Secrets = ["", ""] }.HasValidSecrets());
        Assert.False(new TranscriptionWebhookOptions { Secrets = [valid, valid, valid] }.HasValidSecrets());
        Assert.False(new TranscriptionWebhookOptions { Secrets = ["plain-secret"] }.HasValidSecrets());
        Assert.False(new TranscriptionWebhookOptions { Secrets = ["whsec_not base64"] }.HasValidSecrets());
        Assert.False(new TranscriptionWebhookOptions { Secrets = [""] }.IsConfigured);
    }

    private static TranscriptionWebhookSignature Verifier(params byte[][] keys)
        => new(Options.Create(new TranscriptionWebhookOptions { Secrets = keys.Select(Secret).ToArray() }), new FixedClock());

    private static string Secret(byte[] key) => "whsec_" + Convert.ToBase64String(key);

    private static ReceiveTranscriptionEventInput Input(string signature)
        => new(Body, "evt_1", Now.ToUnixTimeSeconds().ToString(), signature);

    private static string Sign(byte[] key, string? timestamp = null)
    {
        var prefix = Encoding.UTF8.GetBytes($"evt_1.{timestamp ?? Now.ToUnixTimeSeconds().ToString()}.");
        byte[] message = [.. prefix, .. Body];
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, message));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
