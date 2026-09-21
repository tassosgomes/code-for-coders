using FluentValidation;

namespace CodeForCoders.BffAdmin.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed class RecordPlatformHeartbeatInputValidator : AbstractValidator<RecordPlatformHeartbeatInput>
{
    public RecordPlatformHeartbeatInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
    }
}
