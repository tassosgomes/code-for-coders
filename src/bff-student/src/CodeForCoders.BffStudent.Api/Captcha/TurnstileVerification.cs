namespace CodeForCoders.BffStudent.Api.Captcha;

public enum TurnstileOutcome
{
    Passed,
    Failed,
    Unavailable,
}

/// <summary>Result of a siteverify call. <see cref="ErrorCodes"/> carries Cloudflare's error-codes for a failed token.</summary>
public sealed record TurnstileVerification(TurnstileOutcome Outcome, IReadOnlyList<string> ErrorCodes)
{
    public static TurnstileVerification Passed { get; } = new(TurnstileOutcome.Passed, []);

    public static TurnstileVerification Unavailable { get; } = new(TurnstileOutcome.Unavailable, []);

    public static TurnstileVerification Failed(IReadOnlyList<string> errorCodes) => new(TurnstileOutcome.Failed, errorCodes);
}
