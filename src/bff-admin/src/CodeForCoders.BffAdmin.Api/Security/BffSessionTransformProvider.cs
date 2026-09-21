using System.Net.Http.Headers;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Transforms;

namespace CodeForCoders.BffAdmin.Api.Security;

public sealed class BffSessionTransformProvider : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var session = BffSessionContext.Get(transformContext.HttpContext);
            if (session is null)
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return ValueTask.CompletedTask;
            }

            transformContext.ProxyRequest.Headers.Remove("Authorization");
            transformContext.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.UpstreamAccessToken);
            return ValueTask.CompletedTask;
        });
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }
}
