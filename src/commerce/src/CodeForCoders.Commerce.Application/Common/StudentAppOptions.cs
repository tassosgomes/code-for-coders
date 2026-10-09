namespace CodeForCoders.Commerce.Application.Common;

public sealed class StudentAppOptions
{
    /// <summary>Public URL of the student SPA, including its mount path (e.g. https://host/students).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:8082/student";

    public string OrderPageUrl(Guid orderId) => $"{PublicBaseUrl.TrimEnd('/')}/pedidos/{orderId:D}";
}
