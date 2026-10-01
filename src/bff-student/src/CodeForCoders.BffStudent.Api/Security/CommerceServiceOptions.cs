namespace CodeForCoders.BffStudent.Api.Security;

/// <summary>Destination settings for the calls the BFF makes to <c>commerce</c> (ADR-0009): own audience, scopes and key pair.</summary>
public sealed class CommerceServiceOptions
{
    public const string SectionName = "Commerce";

    public string BaseAddress { get; set; } = string.Empty;

    public string Issuer { get; set; } = "bff-student";

    public string Audience { get; set; } = "commerce";

    public string Scope { get; set; } = "showcase:read purchase-intent:write";

    public string SigningKeyId { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;
}
