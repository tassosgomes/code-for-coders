using CodeForCoders.BffAdmin.Api.Endpoints;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        if (app.Configuration.GetValue<bool>("CourseAuthoring:Enabled")) app.MapCourseAuthoringEndpoints();
        app.MapSessionEndpoints();
        app.MapStaffPasswordResetEndpoints();
        app.MapStaffPasswordRecoveryEndpoints();
        app.MapStaffInvitationEndpoints();
        app.MapStaffMemberEndpoints();
        app.MapFinanceAreaEndpoints();
        app.MapCatalogCourseEndpoints();
        app.MapCatalogOfferEndpoints();
        app.MapVideoLibraryEndpoints();
        app.MapVideoUploadEndpoints();
        app.MapAuditRecordEndpoints();
        app.MapStudentAccountEndpoints();
        app.MapProxyEndpoints();
    }
}
