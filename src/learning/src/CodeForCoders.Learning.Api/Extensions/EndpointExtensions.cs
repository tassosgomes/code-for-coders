using CodeForCoders.Learning.Api.Endpoints;

namespace CodeForCoders.Learning.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
    }
}
