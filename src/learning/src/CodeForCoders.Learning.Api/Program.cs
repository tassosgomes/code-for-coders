using CodeForCoders.Learning.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddLearningConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
await app.RunWithVideoProjectionOperationsAsync();

public partial class Program;
