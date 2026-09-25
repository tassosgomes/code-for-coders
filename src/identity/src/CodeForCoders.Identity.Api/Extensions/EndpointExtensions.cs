using CodeForCoders.Identity.Api.Endpoints;

namespace CodeForCoders.Identity.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapStudentAccountEndpoints();
        app.MapStudentConfirmationEndpoints();
        app.MapStudentPasswordRecoveryEndpoints();
        app.MapStudentPasswordChangeEndpoints();
        app.MapStudentSessionEndpoints();
        app.MapStaffSessionEndpoints();
        app.MapSigningKeyEndpoints();
        app.MapStaffPasswordResetEndpoints();
        app.MapStaffInvitationEndpoints();
        app.MapStaffMemberEndpoints();
    }
}
