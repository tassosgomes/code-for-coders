using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases;
using CodeForCoders.Audit.Application.UseCases.Audit.RecordAdministrativeAct;
using CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Audit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordAdministrativeAct>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsSelf()
            .AsMatchingInterface()
            .WithScopedLifetime());
        services.AddScoped<IValidator<SearchAuditRecordsInput>, SearchAuditRecordsInputValidator>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAuditActRecorder>(serviceProvider =>
            serviceProvider.GetRequiredService<RecordAdministrativeAct>());

        return services;
    }
}
