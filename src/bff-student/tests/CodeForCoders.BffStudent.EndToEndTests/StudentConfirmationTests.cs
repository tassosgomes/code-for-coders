using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class StudentConfirmationTests(BffStudentApiFactory factory)
{
    [Fact]
    public async Task StudentConfirmation_ForwardsTokenAndIdempotencyKeyAndReturnsNoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentRegistrationClient.ConfirmationResult = new StudentConfirmationResult(StatusCodes.Status204NoContent, null);
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/api/v1/account-confirmations",
            new StudentAccountConfirmationTokenV1("valid-token"),
            "confirmation-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new StudentAccountConfirmationTokenV1("valid-token"), factory.StudentRegistrationClient.ConfirmationRequest);
        Assert.Equal("confirmation-1", factory.StudentRegistrationClient.IdempotencyKey);
    }

    [Fact]
    public async Task StudentConfirmation_MapsInvalidLinkToPublicProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentRegistrationClient.ConfirmationResult = new StudentConfirmationResult(
            StatusCodes.Status422UnprocessableEntity,
            "INVALID_VERIFICATION_TOKEN");
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/api/v1/account-confirmations",
            new StudentAccountConfirmationTokenV1("bad-token"),
            "confirmation-2");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ConfirmationProblem>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("CONFIRMATION_LINK_INVALID", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task StudentConfirmationRequest_ForwardsEmailAndReturnsNeutralAcceptedResponse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentRegistrationClient.ConfirmationRequestResult = new StudentConfirmationResult(StatusCodes.Status202Accepted, null);
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/api/v1/account-confirmation-requests",
            new StudentAccountConfirmationEmailV1("ana@example.com"),
            "confirmation-request-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(new StudentAccountConfirmationEmailV1("ana@example.com"), factory.StudentRegistrationClient.ConfirmationEmailRequest);
        Assert.Equal("confirmation-request-1", factory.StudentRegistrationClient.IdempotencyKey);
    }

    private static HttpRequestMessage CreateRequest<TBody>(string path, TBody body, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record ConfirmationProblem(string Code, string TraceId);
}
