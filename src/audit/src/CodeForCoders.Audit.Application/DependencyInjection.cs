using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases;
using CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Audit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<IValidator<RecordConsumedAuditEventInput>, RecordConsumedAuditEventInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordConsumedAuditEvent>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());
        services.AddScoped<IAuditEventRecorder, RecordConsumedAuditEvent>();

        return services;
    }
}
