using CodeForCoders.Notification.Api.MessageHandlers;
using CodeForCoders.Notification.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Notification.Api.Extensions;

public static class MessageHandlerExtensions
{
    public static IServiceCollection AddNotificationMessageHandlers(this IServiceCollection services)
    {
        services.AddScoped<INotificationSendRequestMessageHandler, NotificationSendRequestedMessageHandler>();
        return services;
    }
}
