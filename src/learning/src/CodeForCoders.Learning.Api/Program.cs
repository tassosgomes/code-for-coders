using CodeForCoders.Learning.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddLearningConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
