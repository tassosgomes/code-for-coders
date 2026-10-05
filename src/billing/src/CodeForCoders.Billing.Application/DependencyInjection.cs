using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Application.UseCases;
using CodeForCoders.Billing.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Billing.Domain.SeedWork;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Billing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddOptions<BillingProcessingOptions>()
            .BindConfiguration(BillingProcessingOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(options => BillingNamespace.IsValid(options.Namespace),
                "Billing namespace supports letters, digits, '-', '_' and '.' only.")
            .ValidateOnStart();
        services.AddScoped<ITenantContext>(serviceProvider => new TenantContext(
            serviceProvider.GetRequiredService<IOptions<BillingProcessingOptions>>().Value.Namespace));
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
