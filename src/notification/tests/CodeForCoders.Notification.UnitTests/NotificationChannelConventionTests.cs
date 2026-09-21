using CodeForCoders.Notification.Application.Interfaces;
using Xunit;

namespace CodeForCoders.Notification.UnitTests;

public sealed class NotificationChannelConventionTests
{
    [Fact]
    public void FoundationUsesEmailAsItsOnlyNotificationChannel()
        => Assert.Equal([NotificationChannels.Email], NotificationChannels.All);
}
