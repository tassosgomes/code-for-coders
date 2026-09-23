using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;
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
        services.AddScoped<IValidator<ConfirmStudentAccountInput>, ConfirmStudentAccountInputValidator>();
        services.AddScoped<IValidator<RequestStudentAccountConfirmationInput>, RequestStudentAccountConfirmationInputValidator>();
        services.AddScoped<IValidator<RequestStudentPasswordResetInput>, RequestStudentPasswordResetInputValidator>();
        services.AddScoped<IValidator<ResetStudentPasswordInput>, ResetStudentPasswordInputValidator>();
        services.AddScoped<IValidator<AuthenticateStudentSessionInput>, AuthenticateStudentSessionInputValidator>();
        services.AddScoped<IValidator<ValidateStudentSessionInput>, ValidateStudentSessionInputValidator>();
        services.AddScoped<IValidator<RevokeStudentSessionInput>, RevokeStudentSessionInputValidator>();
        services.AddScoped<IStudentRegistrationMessageWriter, StudentRegistrationMessageWriter>();
        services.AddScoped<IStudentConfirmationMessageWriter, StudentConfirmationMessageWriter>();
        services.AddScoped<IStudentPasswordRecoveryMessageWriter, StudentPasswordRecoveryMessageWriter>();
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
