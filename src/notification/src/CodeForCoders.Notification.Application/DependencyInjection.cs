using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.Services;
using CodeForCoders.Notification.Application.UseCases;
using CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;
using CodeForCoders.Notification.Application.UseCases.Platform.RecordPlatformHeartbeat;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.AddScoped<
            IValidator<AcceptNotificationSendRequestInput>,
            AcceptNotificationSendRequestInputValidator>();
        services.AddScoped<IConsentService, TransactionalConsentService>();
        services.AddScoped<IMessageTemplateRenderer, MessageTemplateRenderer>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
