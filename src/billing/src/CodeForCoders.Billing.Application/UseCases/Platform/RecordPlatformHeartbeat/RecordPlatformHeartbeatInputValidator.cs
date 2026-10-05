using FluentValidation;

namespace CodeForCoders.Billing.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed class RecordPlatformHeartbeatInputValidator : AbstractValidator<RecordPlatformHeartbeatInput>
{
    public RecordPlatformHeartbeatInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
    }
}
