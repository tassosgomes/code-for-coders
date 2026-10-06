using System.Net.Http.Headers;
using CodeForCoders.Notification.Api.Security;

namespace CodeForCoders.Notification.Api.Clients;

public sealed class StudentContactAssertionHandler(StudentContactAssertionTokenFactory assertions) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<Guid> TenantKey = new("StudentContactTenant");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.TryGetValue(TenantKey, out var tenant);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertions.Create(tenant));
        return base.SendAsync(request, cancellationToken);
    }
}
