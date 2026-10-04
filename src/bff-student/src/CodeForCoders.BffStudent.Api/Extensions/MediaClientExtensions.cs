using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class MediaClientExtensions
{
    public static IServiceCollection AddMediaClientConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaServiceOptions>().Bind(configuration.GetSection(MediaServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                && options.TimeoutSeconds is >= 1 and <= 5, "Media address and timeout are invalid.").ValidateOnStart();
        services.AddHttpClient<IPlaybackMediaClient, PlaybackMediaClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MediaServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        return services;
    }
}
