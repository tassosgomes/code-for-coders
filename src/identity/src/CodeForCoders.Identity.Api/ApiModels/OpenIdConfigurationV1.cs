using System.Text.Json.Serialization;

namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record OpenIdConfigurationV1(
    string Issuer,
    [property: JsonPropertyName("jwks_uri")] string JwksUri);
