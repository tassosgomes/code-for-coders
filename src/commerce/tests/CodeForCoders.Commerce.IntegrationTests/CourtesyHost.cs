using System.Collections.Concurrent;
using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.IntegrationTests;

/// <summary>
/// Real <c>commerce</c> host for the courtesy and entitlement scenarios: test clock, identity confirmation
/// double and captured logs. It can be shared by the tests of a class; <see cref="Reset"/> gives every
/// test the same starting state a host built only for that test had.
/// </summary>
public sealed class CourtesyHost : IAsyncDisposable
{
    public ConcurrentQueue<string> Logs { get; } = new();
    public CourtesyIdentityConfirmationHandler Identity { get; } = new();
    public CourtesyGrantTestClock Clock { get; } = new();
    public CatalogCourseApiFactory Factory { get; }

    public CourtesyHost(CommerceIntegrationFixture infra, Action<IServiceCollection>? customize = null, bool backgroundWorkers = true)
    {
        Factory = new(infra)
        {
            BackgroundWorkers = backgroundWorkers,
            CustomizeServices = services =>
            {
                services.AddLogging(logging => logging.AddProvider(new CourtesyCapturedLogProvider(Logs)));
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
                CommerceTestHost.UseHandler(services.AddHttpClient<IStudentAccountConfirmationClient, StudentAccountConfirmationClient>(), Identity);
                customize?.Invoke(services);
            }
        };
    }

    /// <summary>Starts the host on first use and restores the per-test state of its test doubles.</summary>
    public void Reset()
    {
        _ = Factory.Server;
        Clock.Now = CourtesyGrantTestClock.Start;
        Identity.Reset();
        // Service assertion issuers come only from each test: none is trusted unless the test configures it.
        Factory.Services.GetRequiredService<IOptions<ServiceAssertionOptions>>().Value.Issuers.Clear();
        Logs.Clear();
    }

    public async ValueTask DisposeAsync() => await Factory.DisposeAsync();
}
