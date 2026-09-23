using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Platform.RecordPlatformHeartbeat;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.AddScoped<IValidator<RegisterStudentAccountInput>, RegisterStudentAccountInputValidator>();
        services.AddScoped<IStudentRegistrationMessageWriter, StudentRegistrationMessageWriter>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IIdempotencyFingerprinter, IdempotencyFingerprinter>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
