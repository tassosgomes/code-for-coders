using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class StudentAccountConfirmationClientTests
{
    [Theory(DisplayName = nameof(RealIdentityWireResponseRequiresMatchingStudentAndBooleanEligibility))]
    [InlineData("eligible")]
    [InlineData("ineligible")]
    [InlineData("wrong-student")]
    [InlineData("missing-student")]
    [InlineData("malformed-student")]
    public async Task RealIdentityWireResponseRequiresMatchingStudentAndBooleanEligibility(string mode)
    {
        using var key = RSA.Create(2048);
        var student = Guid.CreateVersion7();
        object body = mode == "malformed-student" ? new { studentId = 123, eligible = true } : mode == "missing-student" ? new { eligible = true }
            : new { studentId = mode == "wrong-student" ? Guid.CreateVersion7() : student, eligible = mode != "ineligible" };
        using var http = new HttpClient(new StudentConfirmationWireHandler(body)) { BaseAddress = new Uri("http://identity.test/") };
        var settings = Options.Create(new StudentAccountIdentityOptions { SigningKeyId = "test", SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()) });
        var client = new StudentAccountConfirmationClient(http, new StudentAccountAssertionTokenFactory(settings, TimeProvider.System));
        var eligible = await client.ConfirmAsync(Guid.CreateVersion7(), student, TestContext.Current.CancellationToken);
        if (mode is "wrong-student" or "missing-student" or "malformed-student") Assert.Null(eligible);
        else Assert.Equal(mode == "eligible", eligible);
    }
}
