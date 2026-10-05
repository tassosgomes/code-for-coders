using CodeForCoders.Billing.Application.Interfaces;
using Microsoft.Extensions.Options;
namespace CodeForCoders.Billing.Api.Security;

public sealed class PaymentReturnPolicy(IOptions<PaymentReturnOptions> options, IHostEnvironment environment) : IPaymentReturnPolicy
{
    public bool Allows(string url)
     => Uri.TryCreate(url, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
      && options.Value.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
      && (uri.Scheme == "https" || (environment.IsDevelopment() && uri.Scheme == "http" && uri.Host == "localhost"));
}
