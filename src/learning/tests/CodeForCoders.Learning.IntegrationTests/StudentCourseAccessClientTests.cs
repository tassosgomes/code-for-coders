using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using CodeForCoders.Learning.Api.Clients;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class StudentCourseAccessClientTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static StudentCourseAccessClient Client(HttpClient http, RSA key)
    {
        var options = Options.Create(new AccessDecisionOptions
        {
            SigningKeyId = "test",
            SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey())
        });
        return new(http, new(options, TimeProvider.System), options);
    }
    private static StudentCourseAccessQuery Query() => new(Guid.CreateVersion7(), Guid.CreateVersion7());

    [Fact(DisplayName = nameof(SuccessReturnsCoursesWithoutCaching))]
    public async Task SuccessReturnsCoursesWithoutCaching()
    {
        using var key = RSA.Create(2048); var course = Guid.CreateVersion7();
        var boundary = new LessonCommerceBoundaryHandler { CourseAccess = new([new(course, "active", DateTimeOffset.UtcNow, null, null, null)]) };
        using var http = new HttpClient(boundary) { BaseAddress = new("http://commerce.test/"), Timeout = TimeSpan.FromSeconds(2) };
        var client = Client(http, key); var input = Query();
        var result = await client.ListAsync(input, Cancellation); Assert.Equal(course, Assert.Single(result!.Data).CourseId);
        boundary.CourseAccess = new([]); Assert.Empty((await client.ListAsync(input, Cancellation))!.Data);
        Assert.Equal(2, boundary.Calls); Assert.Equal($"/internal/v1/course-access?studentId={input.StudentId:D}", boundary.Path);
    }

    [Fact(DisplayName = nameof(NoResponseIsUnavailableAndNotRetried))]
    public async Task NoResponseIsUnavailableAndNotRetried()
    {
        using var key = RSA.Create(2048); var boundary = new LessonCommerceBoundaryHandler { Timeout = true };
        using var http = new HttpClient(boundary) { BaseAddress = new("http://commerce.test/") };
        Assert.Null(await Client(http, key).ListAsync(Query(), Cancellation)); Assert.Equal(1, boundary.Calls);
    }

    [Fact(DisplayName = nameof(ForbiddenIsUnavailableAndNotAnEmptyList))]
    public async Task ForbiddenIsUnavailableAndNotAnEmptyList()
    {
        using var key = RSA.Create(2048); var boundary = new LessonCommerceBoundaryHandler { Status = HttpStatusCode.Forbidden };
        using var http = new HttpClient(boundary) { BaseAddress = new("http://commerce.test/") };
        Assert.Null(await Client(http, key).ListAsync(Query(), Cancellation)); Assert.Equal(1, boundary.Calls);
    }

    [Fact(DisplayName = nameof(AssertionIsSignedByLearningForCommerceWithCourseAccessScope))]
    public async Task AssertionIsSignedByLearningForCommerceWithCourseAccessScope()
    {
        using var key = RSA.Create(2048); var boundary = new LessonCommerceBoundaryHandler();
        using var http = new HttpClient(boundary) { BaseAddress = new("http://commerce.test/") };
        var input = Query(); Assert.NotNull(await Client(http, key).ListAsync(input, Cancellation));
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(boundary.Assertion, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "learning",
            ValidateAudience = true,
            ValidAudience = "commerce",
            ValidateLifetime = true,
            RequireSignedTokens = true,
            IssuerSigningKey = new RsaSecurityKey(key),
        }, out _);
        Assert.Equal("course-access:read", principal.FindFirst("scope")!.Value);
        Assert.Equal(input.TenantId.ToString(), principal.FindFirst("tenantId")!.Value);
        Assert.Equal("learning", principal.FindFirst("sub")!.Value);
        Assert.NotNull(principal.FindFirst("jti"));
    }

    [Fact(DisplayName = nameof(DecisionAssertionRetainsItsOwnScope))]
    public void DecisionAssertionRetainsItsOwnScope()
    {
        using var key = RSA.Create(2048); var options = Options.Create(new AccessDecisionOptions
        {
            SigningKeyId = "test",
            SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey())
        });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(new AccessDecisionAssertionFactory(options, TimeProvider.System).Create(Guid.CreateVersion7(), "access-decision:read"));
        Assert.Equal("access-decision:read", token.Claims.Single(claim => claim.Type == "scope").Value);
    }
}
