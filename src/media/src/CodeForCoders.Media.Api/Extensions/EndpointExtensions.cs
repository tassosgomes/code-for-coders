using CodeForCoders.Media.Api.Endpoints;

namespace CodeForCoders.Media.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
    }
}
