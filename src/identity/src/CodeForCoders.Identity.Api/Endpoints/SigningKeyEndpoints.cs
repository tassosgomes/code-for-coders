using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class SigningKeyEndpoints
{
    public static void MapSigningKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/jwks", (UserTokenSigningKeySet signingKeys) =>
                Results.Ok(signingKeys.GetPublicKeys()))
            .AllowAnonymous()
            .WithName("GetUserTokenSigningKeysInternal")
            .WithTags("SigningKeys")
            .Produces<JsonWebKeySetResponse>(StatusCodes.Status200OK);
    }
}
