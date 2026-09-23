using CodeForCoders.Identity.Api.Endpoints;

namespace CodeForCoders.Identity.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapStudentAccountEndpoints();
        app.MapStudentConfirmationEndpoints();
        app.MapStudentSessionEndpoints();
    }
}
