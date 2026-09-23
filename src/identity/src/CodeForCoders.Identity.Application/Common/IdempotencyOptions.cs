namespace CodeForCoders.Identity.Application.Common;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public string FingerprintKeyBase64 { get; set; } = string.Empty;
}
