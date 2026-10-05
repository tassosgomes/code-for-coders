namespace CodeForCoders.Commerce.IntegrationTests;

/// <summary>
/// Real <c>commerce</c> hosts shared by the tests of one class (<c>IClassFixture</c>), one per host
/// configuration, built on first use and disposed with the class. Building a host per test dominated the
/// suite time. Data stays isolated as before: every test works on its own new tenant. Scenarios that inject
/// faults or need their own database or broker topology still build a host of their own.
/// </summary>
public sealed class CommerceHosts(CommerceIntegrationFixture infra) : IAsyncDisposable
{
    private CatalogCourseApiFactory? catalog;
    private CatalogCourseApiFactory? catalogWithWorkers;
    private CourtesyHost? courtesy;
    private ShowcaseApiFactory? showcase;

    /// <summary>Host for HTTP and persistence scenarios, without the commerce background workers.</summary>
    public CatalogCourseApiFactory Catalog => catalog ??= new(infra) { BackgroundWorkers = false };

    /// <summary>Host with every commerce worker, for the scenarios that deliver through the broker.</summary>
    public CatalogCourseApiFactory CatalogWithWorkers => catalogWithWorkers ??= new(infra);

    /// <summary>Courtesy host without background workers; each test resets it through its fixture.</summary>
    public CourtesyHost Courtesy => courtesy ??= new(infra, backgroundWorkers: false);

    /// <summary>Showcase host whose <c>bff-student</c> issuer trusts exactly the given schools.</summary>
    public ShowcaseApiFactory Showcase(params Guid[] tenants)
        => (showcase ??= new(infra, Guid.CreateVersion7())).AllowTenants(tenants);

    public async ValueTask DisposeAsync()
    {
        var disposals = new List<Task>();
        if (catalog is not null) disposals.Add(catalog.DisposeAsync().AsTask());
        if (catalogWithWorkers is not null) disposals.Add(catalogWithWorkers.DisposeAsync().AsTask());
        if (courtesy is not null) disposals.Add(courtesy.DisposeAsync().AsTask());
        if (showcase is not null) disposals.Add(showcase.DisposeAsync().AsTask());
        await Task.WhenAll(disposals);
    }
}
