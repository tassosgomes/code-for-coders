using CodeForCoders.Audit.Api.Endpoints;

namespace CodeForCoders.Audit.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapAuditSmokeEndpoints();
    }
}
