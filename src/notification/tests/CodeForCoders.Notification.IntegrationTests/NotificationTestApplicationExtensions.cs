using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Notification.IntegrationTests;

internal static class NotificationTestApplicationExtensions
{
    public static IServiceCollection AddNotificationTestApplication(this IServiceCollection services)
    {
        services.AddApplicationConfiguration();
        services.AddSingleton<IStudentContactClient, UnusedStudentContactClient>();
        return services;
    }
}
