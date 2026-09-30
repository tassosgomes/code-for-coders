using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class CourseReferenceTests : IAsyncLifetime
{
    private readonly Testcontainers.PostgreSql.PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18").Build();
    private readonly Testcontainers.RabbitMq.RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.3-management-alpine").WithUsername("course-reference").WithPassword("course-reference-tests").Build();
    private IHost host = null!;
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private MediaDbContext Context() => new(new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(postgres.GetConnectionString()).Options, new TenantContext());

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(Cancellation), rabbit.StartAsync(Cancellation));
        await using var context = Context(); await context.Database.MigrateAsync(Cancellation);
        host = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()));
            services.AddScoped<CourseReferenceStore>(); services.AddSingleton<RabbitMqConnectionProvider>();
            services.AddSingleton(Options.Create(new RabbitMqOptions { Host = rabbit.Hostname, Port = rabbit.GetMappedPublicPort(5672), Username = "course-reference", Password = "course-reference-tests" }));
            services.AddHostedService<RabbitMqTopologyInitializer>(); services.AddHostedService<CourseReferenceConsumerWorker>();
        }).Build(); await host.StartAsync(Cancellation);
    }

    [Fact(DisplayName = nameof(WorkerStoresOnlyOpaqueIdsWithoutVideoForeignKey))]
    public async Task WorkerStoresOnlyOpaqueIdsWithoutVideoForeignKey()
    {
        var fact = Fact(); await PublishAsync(fact); await WaitAsync(fact);
        await using var context = Context(); var reference = await context.CourseVideoReferences.IgnoreQueryFilters().SingleAsync(row => row.CourseId == fact.CourseId, Cancellation);
        Assert.Equal(fact.References[0].VideoId, reference.VideoId); Assert.Equal(fact.TenantId, reference.TenantId);
        Assert.Empty(await context.Videos.IgnoreQueryFilters().ToListAsync(Cancellation));
        Assert.Equal(4, typeof(CodeForCoders.Media.Infra.Data.CourseReferences.CourseVideoReference).GetProperties().Length);
    }

    [Fact(DisplayName = nameof(DuplicatePublicationDoesNotDuplicateReferences))]
    public async Task DuplicatePublicationDoesNotDuplicateReferences()
    {
        var fact = Fact(); await PublishAsync(fact); await PublishAsync(fact); await WaitAsync(fact);
        await using var context = Context(); Assert.Equal(1, await context.CourseVideoReferences.IgnoreQueryFilters().CountAsync(row => row.CourseId == fact.CourseId, Cancellation));
    }

    [Fact(DisplayName = nameof(OlderPublicationCannotRevertTheCurrentReferences))]
    public async Task OlderPublicationCannotRevertTheCurrentReferences()
    {
        var first = Fact(); var newer = first with { EventId = Guid.CreateVersion7(), VersionNumber = 2, References = [(Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(newer); await WaitAsync(newer); await PublishAsync(first);
        await DrainAsync(); await using var context = Context();
        var reference = await context.CourseVideoReferences.IgnoreQueryFilters().SingleAsync(row => row.CourseId == first.CourseId, Cancellation);
        Assert.Equal(newer.References[0].VideoId, reference.VideoId);
    }

    [Fact(DisplayName = nameof(NewerPublicationAtomicallyReplacesAllPreviousReferences))]
    public async Task NewerPublicationAtomicallyReplacesAllPreviousReferences()
    {
        var first = Fact(); await PublishAsync(first); await WaitAsync(first);
        var newer = first with { EventId = Guid.CreateVersion7(), VersionNumber = 3, References = [(Guid.CreateVersion7(), Guid.CreateVersion7()), (Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(newer); await WaitAsync(newer);
        await using var context = Context(); var references = await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId).ToListAsync(Cancellation);
        Assert.Equal(2, references.Count); Assert.DoesNotContain(references, row => row.LessonId == first.References[0].LessonId);
    }

    [Fact(DisplayName = nameof(SameCourseIdInDifferentTenantsHasIndependentReferenceVersions))]
    public async Task SameCourseIdInDifferentTenantsHasIndependentReferenceVersions()
    {
        var first = Fact(); var other = first with { EventId = Guid.CreateVersion7(), TenantId = Guid.CreateVersion7(), References = [(Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(first); await PublishAsync(other); await WaitAsync(first); await WaitAsync(other);
        await using var context = Context(); Assert.Equal(2, await context.CourseReferenceVersions.IgnoreQueryFilters().CountAsync(row => row.CourseId == first.CourseId, Cancellation));
    }

    [Fact(DisplayName = nameof(MalformedPublicationGoesToDurableDeadLetterQueue))]
    public async Task MalformedPublicationGoesToDurableDeadLetterQueue()
    {
        var provider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
        await using var channel = await provider.CreatePublisherChannelAsync(Cancellation);
        await channel.BasicPublishAsync("learning.events", PublishedCourseFact.Route, true, new BasicProperties { MessageId = Guid.CreateVersion7().ToString() }, Encoding.UTF8.GetBytes("{}"), Cancellation);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var message = await channel.BasicGetAsync("media.course-publications.dlq", true, Cancellation);
            if (message is not null) { Assert.Equal("{}", Encoding.UTF8.GetString(message.Body.Span)); return; }
            await Task.Delay(50, Cancellation);
        }
        Assert.Fail("No dead letter received.");
    }

    private static PublishedCourseFact Fact() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, DateTimeOffset.UtcNow, [(Guid.CreateVersion7(), Guid.CreateVersion7())]);
    private async Task PublishAsync(PublishedCourseFact fact)
    {
        var payload = new { fact.EventId, fact.TenantId, fact.CourseId, fact.VersionNumber, fact.PublishedAt, PublishedById = Guid.CreateVersion7(), Title = "Title must be discarded", Modules = new[] { new { ModuleId = Guid.CreateVersion7(), Title = "Module", Position = 1, Lessons = fact.References.Select((reference, index) => new { reference.LessonId, reference.VideoId, Title = "Lesson", Position = index + 1 }) } } };
        await using var channel = await host.Services.GetRequiredService<RabbitMqConnectionProvider>().CreatePublisherChannelAsync(Cancellation);
        await channel.BasicPublishAsync("learning.events", PublishedCourseFact.Route, true, new BasicProperties { MessageId = fact.EventId.ToString(), ContentType = "application/json" }, JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Cancellation);
    }

    private async Task WaitAsync(PublishedCourseFact fact)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            await using var context = Context();
            if (await context.CourseReferenceVersions.IgnoreQueryFilters().AnyAsync(row => row.CourseId == fact.CourseId && row.TenantId == fact.TenantId && row.VersionNumber == fact.VersionNumber, Cancellation)) return;
            await Task.Delay(50, Cancellation);
        }
        Assert.Fail("Publication did not reach the reference projection.");
    }

    private async Task DrainAsync()
    {
        await using var channel = await host.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await channel.MessageCountAsync("media.course-publications", Cancellation) == 0) { await Task.Delay(100, Cancellation); return; }
            await Task.Delay(50, Cancellation);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (host is not null) { await host.StopAsync(); host.Dispose(); }
        await rabbit.DisposeAsync(); await postgres.DisposeAsync();
    }
}
