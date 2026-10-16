namespace CodeForCoders.BffAdmin.Api.Captcha;

public interface ITurnstileVerifier
{
    Task<TurnstileVerification> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken);
}
