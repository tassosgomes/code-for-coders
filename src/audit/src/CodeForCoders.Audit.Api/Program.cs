using CodeForCoders.Audit.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddAuditConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
