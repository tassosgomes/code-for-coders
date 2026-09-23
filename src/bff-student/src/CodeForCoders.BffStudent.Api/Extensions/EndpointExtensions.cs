using CodeForCoders.BffStudent.Api.Endpoints;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapSessionEndpoints();
        app.MapStudentRegistrationEndpoints();
        app.MapProxyEndpoints();
    }
}
