namespace CodeForCoders.BffStudent.Api.Captcha;

public interface ITurnstileVerifier
{
    Task<TurnstileVerification> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken);
}
