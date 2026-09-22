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

        When(
            input => input.Request.Dados is not null,
            () =>
            {
                RuleFor(input => input.Request.Dados!.Nome)
                    .MaximumLength(255);
                RuleFor(input => input.Request.Dados!.Link)
                    .MaximumLength(2048)
                    .Must(BeAbsoluteUri)
                    .When(input => !string.IsNullOrWhiteSpace(input.Request.Dados!.Link))
                    .WithMessage("Link must be an absolute URI.");
            });
    }

    private static bool BeAbsoluteUri(string? link)
        => Uri.TryCreate(link, UriKind.Absolute, out _);
}
