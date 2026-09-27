using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Media.EndToEndTests;

public sealed class MediaHostRoleTests
{
    [Fact(DisplayName = nameof(ApiRole_DoesNotRegisterWorkerPreparationScans))]
    [Trait("Layer", "Media host roles - End-to-end")]
    public void ApiRole_DoesNotRegisterWorkerPreparationScans()
    {
        var hostedServices = GetHostedServiceTypes("api");

        Assert.Contains(typeof(HeartbeatConsumerWorker), hostedServices);
        Assert.DoesNotContain(typeof(ExpiredVideoUploadWorker), hostedServices);
        Assert.DoesNotContain(typeof(VideoPreparationWorker), hostedServices);
        Assert.False(HasPreparationWorkflow("api"));
    }

    [Fact(DisplayName = nameof(WorkerRole_RegistersPreparationAndExpirationScans))]
    [Trait("Layer", "Media host roles - End-to-end")]
    public void WorkerRole_RegistersPreparationAndExpirationScans()
    {
        var hostedServices = GetHostedServiceTypes("worker");

        Assert.Contains(typeof(ExpiredVideoUploadWorker), hostedServices);
        Assert.Contains(typeof(VideoPreparationWorker), hostedServices);
        Assert.DoesNotContain(typeof(HeartbeatConsumerWorker), hostedServices);
        Assert.True(HasPreparationWorkflow("worker"));
    }

    private static IReadOnlyList<Type> GetHostedServiceTypes(string role)
        => GetMediaServices(role)
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .ToArray();

    private static bool HasPreparationWorkflow(string role)
        => GetMediaServices(role).Any(descriptor => descriptor.ServiceType == typeof(IVideoPreparationWorkflow));

    private static IServiceCollection GetMediaServices(string role)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "EndToEndTest" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=media;Username=media;Password=media",
            ["Media:Role"] = role,
        });
        builder.AddMediaConfiguration();
        return builder.Services;
    }
}
