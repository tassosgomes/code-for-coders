using CodeForCoders.BffAdmin.Api.Security;
using Yarp.ReverseProxy;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddBffProxyConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<BffSessionTransformProvider>();
        return services;
    }
}
