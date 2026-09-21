using CodeForCoders.Notification.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddNotificationConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
