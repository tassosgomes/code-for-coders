using CodeForCoders.Commerce.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
