using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.UseCases;
using CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;
using CodeForCoders.BffAdmin.Application.UseCases.Platform.RecordPlatformHeartbeat;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.BffAdmin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.AddScoped<IValidator<ConfirmAuditRecordComplementInput>, ConfirmAuditRecordComplementInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
