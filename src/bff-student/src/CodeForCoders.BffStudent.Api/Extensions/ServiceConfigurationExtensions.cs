using CodeForCoders.BffStudent.Application;
using CodeForCoders.BffStudent.Infra.Data;
using CodeForCoders.BffStudent.Infra.Messaging;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddBffStudentConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddBffProxyConfiguration(builder.Configuration);
        builder.Services.AddHttpClient("bff-student-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
