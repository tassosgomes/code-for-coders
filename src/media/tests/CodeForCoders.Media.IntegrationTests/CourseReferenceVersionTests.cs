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

public sealed class CourseReferenceVersionTests : IAsyncLifetime
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

    [Fact(DisplayName = nameof(V2RemovesAuthorizationForDeletedLessonAndPreservesRetainedLesson))]
    public async Task V2RemovesAuthorizationForDeletedLessonAndPreservesRetainedLesson()
    {
        var first = Fact() with { References = [(Guid.CreateVersion7(), Guid.CreateVersion7()), (Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(first); await WaitAsync(first);
        var second = first with { EventId = Guid.CreateVersion7(), VersionNumber = 2, References = [first.References[0]] };
        await PublishAsync(second); await WaitAsync(second);
        await using var context = Context();
        var row = Assert.Single(await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId).ToListAsync(Cancellation));
        Assert.Equal(first.References[0].LessonId, row.LessonId); Assert.Equal(first.References[0].VideoId, row.VideoId);
        Assert.False(await context.CourseVideoReferences.IgnoreQueryFilters().AnyAsync(row => row.CourseId == first.CourseId && row.LessonId == first.References[1].LessonId, Cancellation));
    }

    [Fact(DisplayName = nameof(LateV1AfterV2CannotRestoreRemovedReference))]
    public async Task LateV1AfterV2CannotRestoreRemovedReference()
    {
        var first = Fact(); var second = first with { EventId = Guid.CreateVersion7(), VersionNumber = 2, References = [(Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(second); await WaitAsync(second); await PublishAsync(first);
        var barrier = Fact(); await PublishAsync(barrier); await WaitAsync(barrier);
        await using var context = Context();
        var row = Assert.Single(await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId).ToListAsync(Cancellation));
        Assert.Equal(second.References[0].LessonId, row.LessonId);
        Assert.Equal(2, await context.CourseReferenceVersions.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId).Select(row => row.VersionNumber).SingleAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(DuplicateV2AndV1HaveNoEffectAfterV2IsApplied))]
    public async Task DuplicateV2AndV1HaveNoEffectAfterV2IsApplied()
    {
        var first = Fact(); await PublishAsync(first); await WaitAsync(first);
        var second = first with { EventId = Guid.CreateVersion7(), VersionNumber = 2, References = [(first.References[0].LessonId, Guid.CreateVersion7())] };
        await PublishAsync(second); await WaitAsync(second); await PublishAsync(second); await PublishAsync(first);
        var barrier = Fact(); await PublishAsync(barrier); await WaitAsync(barrier);
        await using var context = Context();
        var row = Assert.Single(await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId).ToListAsync(Cancellation));
        Assert.Equal(second.References[0].VideoId, row.VideoId);
    }

    [Fact(DisplayName = nameof(VersionReplacementIsIsolatedToItsTenantAndCourse))]
    public async Task VersionReplacementIsIsolatedToItsTenantAndCourse()
    {
        var first = Fact(); var otherTenant = first with { EventId = Guid.CreateVersion7(), TenantId = Guid.CreateVersion7() }; var otherCourse = Fact() with { TenantId = first.TenantId };
        await PublishAsync(first); await WaitAsync(first); await PublishAsync(otherTenant); await WaitAsync(otherTenant); await PublishAsync(otherCourse); await WaitAsync(otherCourse);
        var second = first with { EventId = Guid.CreateVersion7(), VersionNumber = 2, References = [(Guid.CreateVersion7(), Guid.CreateVersion7())] };
        await PublishAsync(second); await WaitAsync(second);
        await using var context = Context();
        Assert.Equal(first.References[0].VideoId, await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == first.CourseId && row.TenantId == otherTenant.TenantId).Select(row => row.VideoId).SingleAsync(Cancellation));
        Assert.Equal(otherCourse.References[0].VideoId, await context.CourseVideoReferences.IgnoreQueryFilters().Where(row => row.CourseId == otherCourse.CourseId).Select(row => row.VideoId).SingleAsync(Cancellation));
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

    public async ValueTask DisposeAsync()
    {
        if (host is not null) { await host.StopAsync(); host.Dispose(); }
        await rabbit.DisposeAsync(); await postgres.DisposeAsync();
    }
}
