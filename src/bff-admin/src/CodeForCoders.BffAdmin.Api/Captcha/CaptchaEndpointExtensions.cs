namespace CodeForCoders.BffAdmin.Api.Captcha;

public static class CaptchaEndpointExtensions
{
    public static TBuilder RequireCaptcha<TBuilder>(this TBuilder builder, CaptchaFailurePolicy failurePolicy)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(new CaptchaEndpointFilter(failurePolicy));
}
