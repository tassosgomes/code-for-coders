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
        services.AddScoped<IValidator<UseCases.Showcase.ListShowcaseCourses.ListShowcaseCoursesInput>, UseCases.Showcase.ListShowcaseCourses.ListShowcaseCoursesInputValidator>();
        services.AddScoped<IValidator<UseCases.Showcase.RegisterPurchaseIntent.RegisterPurchaseIntentInput>, UseCases.Showcase.RegisterPurchaseIntent.RegisterPurchaseIntentInputValidator>();
        services.AddScoped<IValidator<UseCases.CourtesyGrants.GrantCourtesy.GrantCourtesyInput>, UseCases.CourtesyGrants.GrantCourtesy.GrantCourtesyInputValidator>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IValidator<UseCases.StudentAccessGrants.ListStudentAccessGrants.ListStudentAccessGrantsInput>, UseCases.StudentAccessGrants.ListStudentAccessGrants.ListStudentAccessGrantsInputValidator>();
        services.AddScoped<IValidator<UseCases.CourtesyCourses.ListCourtesyCourses.ListCourtesyCoursesInput>, UseCases.CourtesyCourses.ListCourtesyCourses.ListCourtesyCoursesInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.ResolveOfferReferences.ResolveOfferReferencesInput>, UseCases.CatalogOffers.ResolveOfferReferences.ResolveOfferReferencesInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogCourses.UpdateCatalogCourse.UpdateCatalogCourseInput>, UseCases.CatalogCourses.UpdateCatalogCourse.UpdateCatalogCourseInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.CreateOffer.CreateOfferInput>, UseCases.CatalogOffers.CreateOffer.CreateOfferInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.UpdateOffer.UpdateOfferInput>, UseCases.CatalogOffers.UpdateOffer.UpdateOfferInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.DeleteOffer.DeleteOfferInput>, UseCases.CatalogOffers.DeleteOffer.DeleteOfferInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.PublishOffer.PublishOfferInput>, UseCases.CatalogOffers.PublishOffer.PublishOfferInputValidator>();
        services.AddScoped<IValidator<UseCases.CatalogOffers.UnpublishOffer.UnpublishOfferInput>, UseCases.CatalogOffers.UnpublishOffer.UnpublishOfferInputValidator>();
        services.AddScoped<IValidator<RecordPlatformHeartbeatInput>, RecordPlatformHeartbeatInputValidator>();
        services.Scan(scan => scan
            .FromAssemblyOf<IRecordPlatformHeartbeat>()
            .AddClasses(classes => classes.AssignableTo(typeof(IUseCase<,>)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        return services;
    }
}
