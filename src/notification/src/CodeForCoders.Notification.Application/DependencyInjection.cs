using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.Services;
using CodeForCoders.Notification.Application.UseCases;
using CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;
using CodeForCoders.Notification.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Notification.Domain.SeedWork;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddOptions<NotificationProcessingOptions>()
            .BindConfiguration(NotificationProcessingOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(options => NotificationNamespace.IsValid(options.Namespace),
                "Notification namespace supports letters, digits, '-', '_' and '.' only.")
            .ValidateOnStart();
        services.AddScoped<ITenantContext>(serviceProvider => new TenantContext(
            serviceProvider.GetRequiredService<IOptions<NotificationProcessingOptions>>().Value.Namespace));
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
