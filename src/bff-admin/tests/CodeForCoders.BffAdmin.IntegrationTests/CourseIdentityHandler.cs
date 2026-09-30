using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseIdentityHandler : HttpMessageHandler
{
    public string[] Permissions { get; set; } = ["autoria.ler", "autoria.editar"];
    public bool Revoked { get; set; }
    public string? LastAudience { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var input = await request.Content!.ReadFromJsonAsync<StaffSessionValidationV1>(cancellationToken);
        LastAudience = input?.Audience;
        if (Revoked) return new(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new { code = "SESSION_REQUIRED" }) };
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StaffSessionValidatedV1(
            Guid.Parse("00000000-0000-7000-8000-000000000002"), "Validated teacher", ["professor"], Permissions,
            DateTimeOffset.UtcNow.AddHours(1), input?.Audience == "learning" ? "server-learning-token" : null))
        };
    }
}
