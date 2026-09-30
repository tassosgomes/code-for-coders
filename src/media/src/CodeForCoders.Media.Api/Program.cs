using CodeForCoders.Media.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddMediaConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
await app.RunWithVideoReplayOperationsAsync();

public partial class Program;
