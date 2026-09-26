using System.ComponentModel.DataAnnotations;
using CodeForCoders.Identity.Api.Extensions;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;

namespace CodeForCoders.Identity.Api.Commands;

public static class StaffProvisioningCommand
{
    public static bool IsProvisionFirstAdministrator(string[] args)
        => args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.Ordinal);

    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (!TryParseArguments(args, out var input))
        {
            Console.Error.WriteLine("Usage: provision-first-admin --tenant <id> --email <email> --name <name>");
            return 2;
        }

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.AddIdentityConfiguration();
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var commandInput = input!;
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(commandInput.TenantId);
        var useCase = scope.ServiceProvider.GetRequiredService<IProvisionFirstAdministrator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StaffProvisioningCommand");
        var now = TimeProvider.System.GetUtcNow();
        var output = await useCase.ExecuteAsync(commandInput, CancellationToken.None);

        switch (output.Status)
        {
            case ProvisionFirstAdministratorStatus.Created:
                logger.LogInformation(
                    "First administrator provisioned for tenant {TenantId} at {OccurredOn}.",
                    commandInput.TenantId,
                    now);
                Console.WriteLine("First administrator provisioned. Password setup message queued.");
                return 0;
            case ProvisionFirstAdministratorStatus.AlreadyProvisioned:
                logger.LogInformation(
                    "Tenant {TenantId} already has an administrator at {OccurredOn}.",
                    commandInput.TenantId,
                    now);
                Console.WriteLine("Tenant already has an administrator.");
                return 0;
            case ProvisionFirstAdministratorStatus.EmailBelongsToStudent:
                logger.LogWarning(
                    "First administrator provisioning rejected for tenant {TenantId}: email belongs to a student.",
                    commandInput.TenantId);
                Console.Error.WriteLine("The provided email belongs to a student account.");
                return 1;
            case ProvisionFirstAdministratorStatus.EmailAlreadyInUse:
                logger.LogWarning(
                    "First administrator provisioning rejected for tenant {TenantId}: email is already assigned.",
                    commandInput.TenantId);
                Console.Error.WriteLine("The provided email is already assigned to an internal account.");
                return 1;
            default:
                throw new InvalidOperationException("Unknown first administrator provisioning result.");
        }
    }

    private static bool TryParseArguments(string[] args, out ProvisionFirstAdministratorInput? input)
    {
        input = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length
                || args[index] is not ("--tenant" or "--email" or "--name")
                || !values.TryAdd(args[index], args[index + 1]))
            {
                return false;
            }
        }

        if (!values.TryGetValue("--tenant", out var tenantValue)
            || !Guid.TryParse(tenantValue, out var tenantId)
            || tenantId == Guid.Empty
            || !values.TryGetValue("--email", out var email)
            || !new EmailAddressAttribute().IsValid(email)
            || !values.TryGetValue("--name", out var name)
            || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        input = new ProvisionFirstAdministratorInput(tenantId, email, name);
        return true;
    }

    private const string CommandName = "provision-first-admin";
}
