using CodeForCoders.Identity.Api.Extensions;
using CodeForCoders.Identity.Api.Commands;

if (StaffProvisioningCommand.IsProvisionFirstAdministrator(args))
{
    return await StaffProvisioningCommand.ExecuteAsync(args);
}

var builder = WebApplication.CreateBuilder(args);
builder.AddIdentityConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
await app.RunAsync();
return 0;

public partial class Program;
