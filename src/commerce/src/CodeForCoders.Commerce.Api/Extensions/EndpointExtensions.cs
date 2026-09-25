using CodeForCoders.Commerce.Api.Endpoints;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapFinanceAreaEndpoints();
    }
}
