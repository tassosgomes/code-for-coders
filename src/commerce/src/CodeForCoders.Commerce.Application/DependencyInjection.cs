using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases;
using CodeForCoders.Commerce.Application.UseCases.Platform.RecordPlatformHeartbeat;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Commerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<IValidator<UseCases.CatalogCourses.ListCatalogCourses.ListCatalogCoursesInput>, UseCases.CatalogCourses.ListCatalogCourses.ListCatalogCoursesInputValidator>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IValidator<UseCases.CatalogCourses.UpdateCatalogCourse.UpdateCatalogCourseInput>, UseCases.CatalogCourses.UpdateCatalogCourse.UpdateCatalogCourseInputValidator>();
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
