using CodeForCoders.Billing.Api.Endpoints;

namespace CodeForCoders.Billing.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
    }
}
