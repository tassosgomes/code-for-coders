using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.PreviewCourtesyTerm;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class CourtesyTermPreviewTests(CommerceIntegrationFixture infra)
{
    [Fact(DisplayName = nameof(PreviewUsesSameTermAsGrantWithoutWriting))]
    public async Task PreviewUsesSameTermAsGrantWithoutWriting()
    {
        await using var test = new CourtesyGrantFixture(infra);
        using var response = await test.Client.GetAsync("/internal/v1/courtesy-term-preview?months=6", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var preview = (await response.Content.ReadFromJsonAsync<CourtesyTermPreview>(TestContext.Current.CancellationToken))!;
        await test.AssertEmptyAsync(); await test.SeedAsync(); using var granted = await test.GrantAsync();
        var grant = (await granted.Content.ReadFromJsonAsync<CourtesyGrant>(TestContext.Current.CancellationToken))!; Assert.Equal(preview.EndsOn, grant.EndsOn); Assert.Equal(preview.ExpiresAt, grant.ExpiresAt);
    }
    [Theory(DisplayName = nameof(InvalidMonthsReturn400AndNeverWrite))]
    [InlineData(0)]
    [InlineData(61)]
    public async Task InvalidMonthsReturn400AndNeverWrite(int months)
    {
        await using var test = new CourtesyGrantFixture(infra); using var response = await test.Client.GetAsync($"/internal/v1/courtesy-term-preview?months={months}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await test.AssertEmptyAsync();
    }
    [Fact(DisplayName = nameof(PreviewRequiresGrantPermission))]
    public async Task PreviewRequiresGrantPermission()
    {
        await using var test = new CourtesyGrantFixture(infra); test.Authorize(permission: "financeiro.ler");
        using var response = await test.Client.GetAsync("/internal/v1/courtesy-term-preview?months=6", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); await test.AssertEmptyAsync();
    }
    [Fact(DisplayName = nameof(InvalidSchoolTimeZonePreventsRealHostStartup))]
    public async Task InvalidSchoolTimeZonePreventsRealHostStartup()
    {
        await using var factory = new CatalogCourseApiFactory(infra) { CustomizeServices = services => services.Configure<CodeForCoders.Commerce.Api.Clients.StudentAccountIdentityOptions>(options => options.SchoolTimeZone = "Invalid/School") };
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }
}
