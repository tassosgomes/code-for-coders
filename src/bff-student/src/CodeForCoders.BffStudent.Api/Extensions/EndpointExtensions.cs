using CodeForCoders.BffStudent.Api.Endpoints;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPlatformEndpoints();
        app.MapOrdersEndpoints();
        app.MapSessionEndpoints();
        app.MapStudentRegistrationEndpoints();
        app.MapStudentConfirmationEndpoints();
        app.MapStudentPasswordRecoveryEndpoints();
        app.MapStudentPasswordChangeEndpoints();
        app.MapShowcaseEndpoints();
        app.MapStudentLessonEndpoints();
        app.MapCourseProgressEndpoints();
        app.MapMyCoursesEndpoints();
        app.MapPlaybackSessionEndpoints();
        app.MapProxyEndpoints();
    }
}
