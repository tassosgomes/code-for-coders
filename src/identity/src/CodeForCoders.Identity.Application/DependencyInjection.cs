using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;
using CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;
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
        services.AddScoped<IValidator<ProvisionFirstAdministratorInput>, ProvisionFirstAdministratorInputValidator>();
        services.AddScoped<IValidator<ResetStaffPasswordInput>, ResetStaffPasswordInputValidator>();
        services.AddScoped<IValidator<AuthenticateStaffSessionInput>, AuthenticateStaffSessionInputValidator>();
        services.AddScoped<IValidator<ValidateStaffSessionInput>, ValidateStaffSessionInputValidator>();
        services.AddScoped<IValidator<RevokeStaffSessionInput>, RevokeStaffSessionInputValidator>();
        services.AddScoped<IValidator<CreateStaffInvitationInput>, CreateStaffInvitationInputValidator>();
        services.AddScoped<IValidator<ListPendingStaffInvitationsInput>, ListPendingStaffInvitationsInputValidator>();
        services.AddScoped<IValidator<LookupStaffInvitationInput>, LookupStaffInvitationInputValidator>();
        services.AddScoped<IValidator<AcceptStaffInvitationInput>, AcceptStaffInvitationInputValidator>();
        services.AddScoped<IValidator<ChangeStudentPasswordInput>, ChangeStudentPasswordInputValidator>();
        services.AddScoped<IValidator<AuthenticateStudentSessionInput>, AuthenticateStudentSessionInputValidator>();
        services.AddScoped<IValidator<ValidateStudentSessionInput>, ValidateStudentSessionInputValidator>();
        services.AddScoped<IValidator<RevokeStudentSessionInput>, RevokeStudentSessionInputValidator>();
        services.AddScoped<IStudentRegistrationMessageWriter, StudentRegistrationMessageWriter>();
        services.AddScoped<IStudentConfirmationMessageWriter, StudentConfirmationMessageWriter>();
        services.AddScoped<IStudentPasswordRecoveryMessageWriter, StudentPasswordRecoveryMessageWriter>();
        services.AddScoped<IStaffPasswordRecoveryMessageWriter, StaffPasswordRecoveryMessageWriter>();
        services.AddScoped<IStaffInvitationMessageWriter, StaffInvitationMessageWriter>();
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
