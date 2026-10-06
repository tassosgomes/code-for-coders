using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using Xunit;
namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class FinanceStudentsHttpHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool Unavailable { get; set; }
    public bool Missing { get; set; }
    public string? Assertion { get; private set; }
    public string? Session { get; private set; }
    public Guid[]? StudentIds { get; private set; }
    public const string Email = "private.student@finance.test";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/internal/v1/student-account-resolutions", request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain(Email, request.RequestUri.AbsoluteUri); Assertion = request.Headers.Authorization?.Parameter;
        Session = request.Headers.GetValues("X-Staff-Session").Single();
        using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        StudentIds = doc.RootElement.GetProperty("studentIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
        if (Unavailable) throw new HttpRequestException("Identity connection failed.");
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new StudentAccountResolutionList(Missing ? [] : [new(FinanceOrdersHttpHandler.Student, "Finance Student", Email, "disabled")])) };
    }
}
