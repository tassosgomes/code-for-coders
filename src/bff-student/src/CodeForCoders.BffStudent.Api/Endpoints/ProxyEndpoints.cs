using Yarp.ReverseProxy;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class ProxyEndpoints
{
    public static void MapProxyEndpoints(this WebApplication app)
    {
        app.MapReverseProxy();
    }
}
