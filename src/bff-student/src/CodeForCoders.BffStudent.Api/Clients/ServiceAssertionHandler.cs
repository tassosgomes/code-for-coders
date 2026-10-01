using System.Net.Http.Headers;
using CodeForCoders.BffStudent.Api.Security;

namespace CodeForCoders.BffStudent.Api.Clients;

/// <summary>
/// Signs a fresh assertion for every attempt: it sits inside the resilience pipeline, so a retry never
/// reuses a <c>jti</c> that the destination already consumed.
/// </summary>
public sealed class ServiceAssertionHandler(
    ServiceAssertionTokenFactory tokenFactory,
    ServiceAssertionDestination destination) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<string> ScopeKey = new("bff-student.assertion-scope");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(ScopeKey, out var scope) || string.IsNullOrWhiteSpace(scope))
        {
            throw new InvalidOperationException("The request does not declare the service assertion scope.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenFactory.Create(destination, scope));
        return base.SendAsync(request, cancellationToken);
    }
}
