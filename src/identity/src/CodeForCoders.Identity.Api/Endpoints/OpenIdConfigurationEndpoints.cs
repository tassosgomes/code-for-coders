using CodeForCoders.Identity.Api.ApiModels;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class OpenIdConfigurationEndpoints
{
    public static void MapOpenIdConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/openid-configuration", (HttpRequest request) =>
                Results.Ok(new OpenIdConfigurationV1(
                    "identity",
                    $"{request.Scheme}://{request.Host}/internal/v1/jwks")))
            .AllowAnonymous()
            .WithName("GetOpenIdConfiguration")
            .WithTags("SigningKeys")
            .Produces<OpenIdConfigurationV1>(StatusCodes.Status200OK);
    }
}
