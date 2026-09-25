using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace CodeForCoders.BffAdmin.Api.Security;

public sealed class BffSessionTransformProvider : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var accessToken = BffSessionContext.GetValidatedSession(transformContext.HttpContext)?.AccessToken;
            transformContext.ProxyRequest.Headers.Remove("Authorization");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return ValueTask.CompletedTask;
            }

            transformContext.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
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
