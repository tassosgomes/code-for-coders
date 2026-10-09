using CodeForCoders.Commerce.Application.Common;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

[Trait("Application", "StudentAppOptions - Unit")]
public sealed class StudentAppOptionsTests
{
    private static readonly Guid OrderId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    [Theory(DisplayName = nameof(OrderPageUrlKeepsTheConfiguredMount))]
    [InlineData("http://localhost:8082/student", "http://localhost:8082/student/pedidos/01900000-0000-7000-8000-000000000001")]
    [InlineData("https://dev-code4coders.tasso.dev.br/students", "https://dev-code4coders.tasso.dev.br/students/pedidos/01900000-0000-7000-8000-000000000001")]
    [InlineData("https://dev-code4coders.tasso.dev.br/students/", "https://dev-code4coders.tasso.dev.br/students/pedidos/01900000-0000-7000-8000-000000000001")]
    public void OrderPageUrlKeepsTheConfiguredMount(string baseUrl, string expected)
        => Assert.Equal(expected, new StudentAppOptions { PublicBaseUrl = baseUrl }.OrderPageUrl(OrderId));
}
