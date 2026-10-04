using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class LearningClientExtensions
{
    public static IServiceCollection AddLearningClientConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LearningServiceOptions>().Bind(configuration.GetSection(LearningServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                && options.TimeoutSeconds is >= 1 and <= 5, "Learning address and timeout are invalid.").ValidateOnStart();
        services.AddHttpClient<IStudentLessonLearningClient, StudentLessonLearningClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LearningServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        return services;
    }
}
