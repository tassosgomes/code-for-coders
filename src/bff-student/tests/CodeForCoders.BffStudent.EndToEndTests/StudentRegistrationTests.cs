using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class StudentRegistrationTests(BffStudentApiFactory factory)
{
    [Fact]
    public async Task RegisterStudent_ForwardsRequestAndIdempotencyKeyAndReturnsAccepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentRegistrationClient.Result = new StudentRegistrationResult(StatusCodes.Status202Accepted, null);
        using var client = factory.CreateClient();
        using var request = CreateRegistrationRequest("new-student-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("new-student-1", factory.StudentRegistrationClient.IdempotencyKey);
        Assert.Equal("Ana Souza", factory.StudentRegistrationClient.Request?.Name);
        Assert.Equal("ana@example.com", factory.StudentRegistrationClient.Request?.Email);
        Assert.Equal("SenhaForte1!", factory.StudentRegistrationClient.Request?.Password);
    }

    [Fact]
    public async Task RegisterStudent_MapsDuplicateAccountToPublicProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentRegistrationClient.Result = new StudentRegistrationResult(
            StatusCodes.Status422UnprocessableEntity,
            "ACCOUNT_ALREADY_EXISTS");
        using var client = factory.CreateClient();
        using var request = CreateRegistrationRequest("new-student-2");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<RegistrationProblem>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("ACCOUNT_ALREADY_EXISTS", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    private static HttpRequestMessage CreateRegistrationRequest(string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student-accounts")
        {
            Content = JsonContent.Create(new StudentRegistrationRequestV1(
                "Ana Souza",
                "ana@example.com",
                "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record RegistrationProblem(string Code, string TraceId);
}
