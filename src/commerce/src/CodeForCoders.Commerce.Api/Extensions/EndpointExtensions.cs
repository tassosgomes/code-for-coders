using CodeForCoders.Commerce.Api.Endpoints;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapStudentOrderEndpoints();
        app.MapFinanceAreaEndpoints();
        app.MapCatalogCourseEndpoints();
        app.MapCourtesyCourseEndpoints();
        app.MapCourtesyGrantEndpoints();
        app.MapStudentAccessGrantEndpoints();
        app.MapAccessDecisionEndpoints();
        app.MapStudentCourseAccessEndpoints();
        app.MapOfferReferenceEndpoints();
        app.MapShowcaseEndpoints();
        app.MapPurchaseIntentEndpoints();
    }
}
