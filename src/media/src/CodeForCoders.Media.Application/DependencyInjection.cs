using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Media.Application.UseCases.VideoUploads.ExpirePendingVideoUploads;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Media.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IExpirePendingVideoUploads, ExpirePendingVideoUploads>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
