using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Messaging;

namespace CodeForCoders.Audit.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddAuditConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        return builder;
    }
}
