using FluentValidation;

namespace CodeForCoders.BffStudent.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed class RecordPlatformHeartbeatInputValidator : AbstractValidator<RecordPlatformHeartbeatInput>
{
    public RecordPlatformHeartbeatInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
    }
}
