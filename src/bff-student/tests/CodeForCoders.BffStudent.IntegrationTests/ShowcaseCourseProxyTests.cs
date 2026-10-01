using System.Net;
using System.Text.Json;
using Polly.Timeout;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

[Collection(BffStudentIntegrationCollection.Name)]
public sealed class ShowcaseCourseProxyTests(BffStudentIntegrationFixture fixture)
{
    private const string CourseId = "6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e";
    private const string Page = """
        {"courseId":"6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e","title":".NET do zero à API","level":"advanced",
        "description":"Do primeiro programa a uma API publicada.","authorEmail":"leak@example.com",
        "prerequisite":{"text":"Git e C# básico.","recommendedCourses":[{"courseId":"3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c","title":"Fundamentos de C#","inShowcase":true,"videoId":"x"}]},
        "modules":[{"title":"Fundamentos","lessons":[{"title":"Tipos e variáveis","videoId":"leak"}]}],
        "offers":[{"offerId":"7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f","name":"Acesso por 12 meses","priceCents":39700,"accessPeriod":{"type":"months","months":12}},
        {"offerId":"8d9e0f1a-2b3c-4d4e-9f5a-6b7c8d9e0f1a","name":"Acesso vitalício","priceCents":89700,"accessPeriod":{"type":"lifetime"}}]}
        """;
    private const string NotFound = """{"type":"about:blank","title":"Curso não disponível.","status":404,"code":"SHOWCASE_COURSE_NOT_FOUND","traceId":"abc"}""";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(AnonymousVisitorGetsTheCoursePageSignedForCommerceAndWithoutExtraFields))]
    public async Task AnonymousVisitorGetsTheCoursePageSignedForCommerceAndWithoutExtraFields()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.OK, Page);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/showcase/courses/{CourseId}", Cancellation);
        var body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var forwarded = Assert.Single(factory.Commerce.Requests);
        Assert.Equal(HttpMethod.Get, forwarded.Method);
        Assert.Equal($"/internal/v1/showcase/courses/{CourseId}", forwarded.RequestUri!.AbsolutePath);
        Assert.StartsWith("Bearer ", forwarded.Authorization, StringComparison.Ordinal);
        Assert.False(forwarded.HasCookie);
        Assert.Equal(0, factory.Identity.Calls);
        Assert.DoesNotContain("leak", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("videoId", body, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            ["courseId", "description", "level", "modules", "offers", "prerequisite", "title"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var offers = document.RootElement.GetProperty("offers");
        Assert.Equal(12, offers[0].GetProperty("accessPeriod").GetProperty("months").GetInt32());
        Assert.False(offers[1].GetProperty("accessPeriod").TryGetProperty("months", out _));
        Assert.True(document.RootElement.GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("inShowcase").GetBoolean());
    }

    [Fact(DisplayName = nameof(CommerceNotFoundAndAMalformedAddressReachTheSpaAsTheSameNotFound))]
    public async Task CommerceNotFoundAndAMalformedAddressReachTheSpaAsTheSameNotFound()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.NotFound, NotFound);
        using var client = factory.CreateClient();

        var unknown = await GetProblemAsync(client, CourseId);
        var malformed = await GetProblemAsync(client, "not-an-identifier");

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.NotFound, malformed.Status);
        Assert.Equal("SHOWCASE_COURSE_NOT_FOUND", unknown.Code);
        Assert.Equal(unknown.Code, malformed.Code);
        Assert.Equal(unknown.Title, malformed.Title);
        Assert.Single(factory.Commerce.Requests);
    }

    [Theory(DisplayName = nameof(CommerceFailuresBecomeBadGatewayAndNeverLeakTheirCode))]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CommerceFailuresBecomeBadGatewayAndNeverLeakTheirCode(HttpStatusCode commerceStatus)
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(commerceStatus, """{"code":"SERVICE_ASSERTION_INVALID"}""");
        using var client = factory.CreateClient();

        var problem = await GetProblemAsync(client, CourseId);

        Assert.Equal(HttpStatusCode.BadGateway, problem.Status);
        Assert.Equal("SHOWCASE_UNAVAILABLE", problem.Code);
    }

    [Fact(DisplayName = nameof(CommerceTimeoutBecomesGatewayTimeoutWithTheShowcaseCode))]
    public async Task CommerceTimeoutBecomesGatewayTimeoutWithTheShowcaseCode()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => throw new TimeoutRejectedException("attempt timed out");
        using var client = factory.CreateClient();

        var problem = await GetProblemAsync(client, CourseId);

        Assert.Equal(HttpStatusCode.GatewayTimeout, problem.Status);
        Assert.Equal("SHOWCASE_TIMEOUT", problem.Code);
    }

    private static async Task<(HttpStatusCode Status, string? Code, string? Title)> GetProblemAsync(HttpClient client, string courseId)
    {
        using var response = await client.GetAsync($"/api/v1/showcase/courses/{courseId}", Cancellation);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        return (
            response.StatusCode,
            document.RootElement.GetProperty("code").GetString(),
            document.RootElement.GetProperty("title").GetString());
    }
}
