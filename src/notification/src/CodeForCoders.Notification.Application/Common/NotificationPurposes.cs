namespace CodeForCoders.Notification.Application.Common;

public static class NotificationPurposes
{
    public const string AccountConfirmation = "confirmacao-de-conta";

    public const string PasswordRecovery = "recuperacao-de-senha";
}

public static class NotificationRefusalReasons
{
    public const string MissingPurpose = "finalidade ausente";

    public const string UnknownPurpose = "finalidade desconhecida";

    public const string UnknownModel = "modelo desconhecido";

    public const string MissingData = "dado faltante";

    public const string InvalidFormat = "forma inválida";
}

public static class NotificationFailureReasons
{
    public const string ProviderUnavailable = "provedor-indisponivel";

    public const string ProviderTimeout = "timeout-do-provedor";

    public const string PermanentProviderFailure = "recusa-permanente-do-provedor";

    public const string AttemptsExhausted = "tentativas-esgotadas";
}
