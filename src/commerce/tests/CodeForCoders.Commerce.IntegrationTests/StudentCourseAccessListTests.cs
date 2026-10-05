using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class StudentCourseAccessListTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private AccessDecisionFixture Fixture() => new(hosts.Courtesy, options =>
    {
        var trusted = options.Issuers["access-decision-test"];
        options.Issuers["learning"] = new()
        {
            PublicKeys = trusted.PublicKeys,
            AllowedTenantIds = trusted.AllowedTenantIds,
            AllowedScopes = ["access-decision:read", "course-access:read"]
        };
        options.Issuers["media"] = new()
        {
            PublicKeys = trusted.PublicKeys,
            AllowedTenantIds = trusted.AllowedTenantIds,
            AllowedScopes = ["access-decision:read"]
        };
    });

    private static Task<HttpResponseMessage> GetAsync(AccessDecisionFixture test, string scope = "course-access:read",
        string issuer = "learning", Guid? tenant = null, Guid? student = null)
        => test.RequestAsync(test.Assertion(scope, tenant, issuer),
            $"/internal/v1/course-access?studentId={(student ?? test.Courtesy.Student):D}");

    private static async Task<JsonDocument> BodyAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));

    [Theory(DisplayName = nameof(OnlyLearningWithCourseAccessScopeCanReadList))]
    [InlineData("media", "access-decision:read")]
    [InlineData("media", "course-access:read")]
    [InlineData("learning", "access-decision:read")]
    public async Task OnlyLearningWithCourseAccessScopeCanReadList(string issuer, string scope)
    {
        await using var test = Fixture(); using var response = await GetAsync(test, scope, issuer);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = await BodyAsync(response); Assert.Equal("SCOPE_DENIED", body.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(test.Measurements, item => item.StartsWith("commerce.showcase", StringComparison.Ordinal));
    }

    [Fact(DisplayName = nameof(OtherSchoolReceivesAnEmptyList))]
    public async Task OtherSchoolReceivesAnEmptyList()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync();
        using var response = await GetAsync(test, tenant: test.OtherTenant); response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response); Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact(DisplayName = nameof(StudentWithoutGrantsReceivesAnEmptyList))]
    public async Task StudentWithoutGrantsReceivesAnEmptyList()
    {
        await using var test = Fixture(); using var response = await GetAsync(test); response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response); Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact(DisplayName = nameof(TwoActiveGrantsProduceOneCourseWithMostRecentActiveSince))]
    public async Task TwoActiveGrantsProduceOneCourseWithMostRecentActiveSince()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync();
        test.Courtesy.Clock.Now = test.Courtesy.Clock.Now.AddDays(1); await test.GrantAsync();
        using var response = await GetAsync(test); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        var data = body.RootElement.GetProperty("data"); Assert.Single(data.EnumerateArray());
        Assert.Equal("active", data[0].GetProperty("status").GetString());
        Assert.Equal(test.Courtesy.Clock.Now, data[0].GetProperty("since").GetDateTimeOffset());
    }

    [Fact(DisplayName = nameof(ExpiredAndLifetimeGrantsRemainActive))]
    public async Task ExpiredAndLifetimeGrantsRemainActive()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync(1);
        test.Courtesy.Clock.Now = test.Courtesy.Clock.Now.AddMonths(2); await test.GrantAsync(type: "lifetime");
        using var response = await GetAsync(test); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        Assert.Equal("active", body.RootElement.GetProperty("data")[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("data")[0].GetProperty("endedAt").ValueKind);
    }

    [Fact(DisplayName = nameof(OnlyExpiredGrantsUseLatestExclusiveExpiryAndSchoolDate))]
    public async Task OnlyExpiredGrantsUseLatestExclusiveExpiryAndSchoolDate()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync(1);
        var latest = await test.GrantAsync(2); test.Courtesy.Clock.Now = latest.ExpiresAt!.Value;
        using var response = await GetAsync(test); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        var item = body.RootElement.GetProperty("data")[0]; Assert.Equal("ended", item.GetProperty("status").GetString());
        Assert.Equal(latest.EndsOn!.Value.ToString("yyyy-MM-dd"), item.GetProperty("endedOn").GetString());
        Assert.Equal(latest.ExpiresAt, item.GetProperty("endedAt").GetDateTimeOffset());
        Assert.Equal("grant-ended", item.GetProperty("endedReason").GetString()); Assert.Equal(JsonValueKind.Null, item.GetProperty("since").ValueKind);
    }

    [Fact(DisplayName = nameof(ResponseContainsOnlyContractFieldsAndCannotBeCached))]
    public async Task ResponseContainsOnlyContractFieldsAndCannotBeCached()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync();
        using var response = await GetAsync(test); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        Assert.Equal(["data"], body.RootElement.EnumerateObject().Select(item => item.Name));
        Assert.Equal(["courseId", "status", "since", "endedOn", "endedAt", "endedReason"],
            body.RootElement.GetProperty("data")[0].EnumerateObject().Select(item => item.Name));
        Assert.Equal(test.Courtesy.Course, body.RootElement.GetProperty("data")[0].GetProperty("courseId").GetGuid());
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
    }

    [Fact(DisplayName = nameof(InactiveGrantNeverConfersAccess))]
    public async Task InactiveGrantNeverConfersAccess()
    {
        await using var test = Fixture(); await test.Courtesy.SeedAsync(); await test.GrantAsync();
        await using var serviceScope = test.Courtesy.Factory.Services.CreateAsyncScope();
        serviceScope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Courtesy.Tenant);
        var db = serviceScope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE entitlement.access_grants SET status = 'ended' WHERE tenant_id = {test.Courtesy.Tenant}
            """, Cancellation);
        using var response = await GetAsync(test); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        Assert.Equal("ended", body.RootElement.GetProperty("data")[0].GetProperty("status").GetString());
    }

    [Fact(DisplayName = nameof(CourseAccessScopeCannotReadAccessDecision))]
    public async Task CourseAccessScopeCannotReadAccessDecision()
    {
        await using var test = Fixture(); using var response = await test.RequestAsync(test.Assertion("course-access:read", issuer: "learning"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = nameof(EmptyStudentIsRejected))]
    public async Task EmptyStudentIsRejected()
    {
        await using var test = Fixture();
        using var response = await test.RequestAsync(test.Assertion("course-access:read", issuer: "learning"),
            $"/internal/v1/course-access?studentId={Guid.Empty}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = nameof(GrantOfAnotherStudentInSameSchoolAndCourseNeverAppearsInList))]
    public async Task GrantOfAnotherStudentInSameSchoolAndCourseNeverAppearsInList()
    {
        await using var test = Fixture();
        await test.Courtesy.SeedAsync();
        var otherStudent = Guid.CreateVersion7();
        await test.GrantAsync(studentId: otherStudent);

        using var response = await GetAsync(test);
        response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response);
        Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());

        using var otherResponse = await GetAsync(test, student: otherStudent);
        otherResponse.EnsureSuccessStatusCode();
        using var otherBody = await BodyAsync(otherResponse);
        var otherData = otherBody.RootElement.GetProperty("data");
        Assert.Single(otherData.EnumerateArray());
        Assert.Equal(test.Courtesy.Course, otherData[0].GetProperty("courseId").GetGuid());
        Assert.Equal("active", otherData[0].GetProperty("status").GetString());

        var expired = await test.GrantAsync(1);
        test.Courtesy.Clock.Now = expired.ExpiresAt!.Value;

        using var studentResponse = await GetAsync(test);
        studentResponse.EnsureSuccessStatusCode();
        using var studentBody = await BodyAsync(studentResponse);
        var studentData = studentBody.RootElement.GetProperty("data");
        Assert.Single(studentData.EnumerateArray());
        Assert.Equal(test.Courtesy.Course, studentData[0].GetProperty("courseId").GetGuid());
        Assert.Equal("ended", studentData[0].GetProperty("status").GetString());
        Assert.Equal(expired.ExpiresAt, studentData[0].GetProperty("endedAt").GetDateTimeOffset());
    }
}
