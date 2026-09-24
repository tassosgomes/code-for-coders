using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Infra.Data;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using CodeForCoders.BffStudent.Infra.Data.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

[Collection(BffStudentIntegrationCollection.Name)]
public sealed class ValkeyBffSessionStoreTests(BffStudentIntegrationFixture fixture) : IAsyncDisposable
{
    private const string KeyPrefix = "bff-student-integration:session:";

    private readonly ValkeyConnectionProvider connectionProvider = new(
        Options.Create(new ValkeyOptions { ConnectionString = fixture.ValkeyConnectionString }));

    [Fact]
    public async Task StoreAsync_KeepsOpaqueSessionUnderPrefixedKeyUntilItsExpiration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var cookieValue = NewCookieValue();
        var session = NewSession(TimeSpan.FromMinutes(10));

        await store.StoreAsync(cookieValue, session, cancellationToken);

        var stored = await store.GetAsync(cookieValue, cancellationToken);
        Assert.Equal(session, stored);
        var database = (await connectionProvider.GetAsync(cancellationToken)).GetDatabase();
        var ttl = await database.KeyTimeToLiveAsync($"{KeyPrefix}{cookieValue}");
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
        Assert.False(await database.KeyExistsAsync(cookieValue));
    }

    [Fact]
    public async Task StoreAsync_RenewsTtlWhenTheSameSessionIsStoredAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var cookieValue = NewCookieValue();
        var session = NewSession(TimeSpan.FromMinutes(1));
        await store.StoreAsync(cookieValue, session, cancellationToken);

        var renewed = session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) };
        await store.StoreAsync(cookieValue, renewed, cancellationToken);

        Assert.Equal(renewed, await store.GetAsync(cookieValue, cancellationToken));
        var database = (await connectionProvider.GetAsync(cancellationToken)).GetDatabase();
        var ttl = await database.KeyTimeToLiveAsync($"{KeyPrefix}{cookieValue}");
        Assert.NotNull(ttl);
        Assert.True(ttl.Value > TimeSpan.FromMinutes(29));
    }

    [Fact]
    public async Task StoreAsync_DoesNotWriteSessionThatIsAlreadyExpired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var cookieValue = NewCookieValue();

        await store.StoreAsync(cookieValue, NewSession(TimeSpan.FromSeconds(-1)), cancellationToken);

        Assert.Null(await store.GetAsync(cookieValue, cancellationToken));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForUnknownCookie()
    {
        var store = CreateStore();

        var stored = await store.GetAsync(NewCookieValue(), TestContext.Current.CancellationToken);

        Assert.Null(stored);
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyTheCurrentSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var currentCookie = NewCookieValue();
        var otherCookie = NewCookieValue();
        var otherSession = NewSession(TimeSpan.FromMinutes(10));
        await store.StoreAsync(currentCookie, NewSession(TimeSpan.FromMinutes(10)), cancellationToken);
        await store.StoreAsync(otherCookie, otherSession, cancellationToken);

        await store.RemoveAsync(currentCookie, cancellationToken);
        await store.RemoveAsync(currentCookie, cancellationToken);

        Assert.Null(await store.GetAsync(currentCookie, cancellationToken));
        Assert.Equal(otherSession, await store.GetAsync(otherCookie, cancellationToken));
    }

    [Fact]
    public async Task ValkeyHealthCheck_ReportsHealthyWhenValkeyAnswersPing()
    {
        var result = await new ValkeyHealthCheck(connectionProvider).CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    public async ValueTask DisposeAsync()
        => await connectionProvider.DisposeAsync();

    private ValkeyBffSessionStore CreateStore()
        => new(
            connectionProvider,
            Options.Create(new BffSecurityOptions { SessionKeyPrefix = KeyPrefix }),
            TimeProvider.System);

    private static string NewCookieValue()
        => Convert.ToHexString(Guid.CreateVersion7().ToByteArray());

    private static OpaqueBffSession NewSession(TimeSpan lifetime)
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ana Souza",
            Convert.ToHexString(Guid.CreateVersion7().ToByteArray()),
            DateTimeOffset.UtcNow.Add(lifetime));
}
