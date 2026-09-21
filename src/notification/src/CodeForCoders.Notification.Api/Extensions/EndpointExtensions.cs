using CodeForCoders.Notification.Api.Endpoints;

namespace CodeForCoders.Notification.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
    }
}
