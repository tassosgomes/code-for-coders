namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record JsonWebKeySetResponse(IReadOnlyList<JsonWebKeyResponse> Keys);

public sealed record JsonWebKeyResponse(
    string Kty,
    string Use,
    string Alg,
    string Kid,
    string N,
    string E);
