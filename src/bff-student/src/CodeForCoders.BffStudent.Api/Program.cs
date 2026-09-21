using CodeForCoders.BffStudent.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddBffStudentConfiguration();

var app = builder.Build();
app.UseApplicationPipeline();
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
