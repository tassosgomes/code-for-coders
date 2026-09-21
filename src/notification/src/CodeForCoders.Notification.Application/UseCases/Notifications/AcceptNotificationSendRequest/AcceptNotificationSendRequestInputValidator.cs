using CodeForCoders.Notification.Application.Common;
using FluentValidation;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;

public sealed class AcceptNotificationSendRequestInputValidator
    : AbstractValidator<AcceptNotificationSendRequestInput>
{
    public AcceptNotificationSendRequestInputValidator()
    {
        RuleFor(input => input.Request.PedidoId)
            .NotEmpty();
        RuleFor(input => input.Request.TenantId)
            .NotEmpty();
        RuleFor(input => input.Request.Destinatario)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
        RuleFor(input => input.Request.Finalidade)
            .Equal(NotificationPurposes.AccountConfirmation);
        RuleFor(input => input.Request.Modelo)
            .Equal(NotificationPurposes.AccountConfirmation);
        RuleFor(input => input.Request.Dados)
            .NotNull();
        When(
            input => input.Request.Dados is not null,
            () =>
            {
                RuleFor(input => input.Request.Dados!.Nome)
                    .NotEmpty()
                    .MaximumLength(255);
                RuleFor(input => input.Request.Dados!.Link)
                    .NotEmpty()
                    .MaximumLength(2048)
                    .Must(BeAbsoluteUri)
                    .WithMessage("Link must be an absolute URI.");
            });
    }

    private static bool BeAbsoluteUri(string? link)
        => Uri.TryCreate(link, UriKind.Absolute, out _);
}
