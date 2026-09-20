using CodeForCoders.Identity.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddIdentityConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
