using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Billing.Application.Common;

public sealed class BillingProcessingOptions
{
    public const string SectionName = "Billing";
    public const string DefaultNamespace = "default";

    [Required]
    public string Namespace { get; set; } = DefaultNamespace;
}
