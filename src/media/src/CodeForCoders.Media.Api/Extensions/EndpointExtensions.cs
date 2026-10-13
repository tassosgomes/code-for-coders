using CodeForCoders.Media.Api.Endpoints;
using CodeForCoders.Media.Application.Common;
using Microsoft.Extensions.Options;
using CodeForCoders.Media.Infra.Messaging.Configuration;

namespace CodeForCoders.Media.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        if (MediaRoleOptions.ReadRole(app.Configuration) != MediaServiceRole.Api)
        {
            return;
        }

        app.MapPlatformEndpoints();
        app.MapVideoEndpoints();
        app.MapPlaybackSessionEndpoints();
        app.MapVideoUploadEndpoints();
        if (app.Services.GetRequiredService<IOptions<TranscriptionWebhookOptions>>().Value.IsConfigured)
            app.MapTranscriptionEventEndpoints();
    }
}
