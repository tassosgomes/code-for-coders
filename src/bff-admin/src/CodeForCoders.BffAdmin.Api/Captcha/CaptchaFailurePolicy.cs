namespace CodeForCoders.BffAdmin.Api.Captcha;

/// <summary>What to do when the siteverify is unreachable (ADR-0023): logins stay open; account creation and e-mail triggers close.</summary>
public enum CaptchaFailurePolicy
{
    FailOpen,
    FailClosed,
}
