using CodeForCoders.BffAdmin.Api.Endpoints;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapSessionEndpoints();
        app.MapStaffPasswordResetEndpoints();
        app.MapStaffPasswordRecoveryEndpoints();
        app.MapStaffInvitationEndpoints();
        app.MapStaffMemberEndpoints();
        app.MapFinanceAreaEndpoints();
        app.MapProxyEndpoints();
    }
}
