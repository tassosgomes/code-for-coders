using CodeForCoders.BffAdmin.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddBffAdminConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
