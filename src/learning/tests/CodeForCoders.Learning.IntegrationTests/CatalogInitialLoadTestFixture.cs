using System.Text.Json;
using CodeForCoders.Learning.Application;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.Outbox;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CatalogInitialLoadTestFixture(LearningIntegrationFixture infrastructure)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private readonly string instance = Guid.CreateVersion7().ToString("N");
    public string Exchange => "catalog-test." + instance;

    public LearningDbContext Context()
        => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(infrastructure.PostgreSql.GetConnectionString()).Options, new TenantContext());

    public async Task ResetAsync()
    {
        await using var context = Context();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE content.courses, content.outbox_messages, content.catalog_initial_load_executions CASCADE", Cancellation);
    }

    public IHost CreateHost(IReadOnlyDictionary<string, string?> settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = infrastructure.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = infrastructure.RabbitMq.Hostname,
            ["RabbitMq:Port"] = infrastructure.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:AuditExchange"] = Exchange + ".audit",
            ["RabbitMq:DeadLetterExchange"] = Exchange + ".dlx",
            ["RabbitMq:HeartbeatQueue"] = Exchange + ".heartbeat",
            ["RabbitMq:VideoFactsQueue"] = Exchange + ".video",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
        };
        foreach (var (key, value) in settings) values[key] = value;
        return Host.CreateDefaultBuilder().UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values))
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
            }).Build();
    }

    public IHost EnabledHost() => CreateHost(new Dictionary<string, string?> { ["CatalogInitialLoad:Enabled"] = "true" });

    public static async Task RunAsync(IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogInitialLoad>().RunAsync(Cancellation);
    }

    public async Task<Course> AddDraftAsync(Guid tenant)
    {
        var course = Course.Create(new(tenant, Guid.CreateVersion7(), "Private teacher", "Published title", "Published description", DateTimeOffset.UtcNow));
        var module = course.AddModule(new("Published module", null, false, null, null));
        course.AddLesson(module, new("Published lesson", null, false, null, null, Guid.CreateVersion7(), true));
        await using var context = Context();
        context.Courses.Add(course);
        await context.SaveChangesAsync(Cancellation);
        return course;
    }

    public async Task<CourseVersion> AddPublishedAsync(Guid tenant)
    {
        var course = await AddDraftAsync(tenant);
        await using var context = Context();
        var tracked = await context.Courses.IgnoreQueryFilters().Include(item => item.Modules).ThenInclude(module => module.Lessons)
            .SingleAsync(item => item.Id == course.Id, Cancellation);
        tracked.Update(new(null, null, false, null, null, Level: "beginner", HasLevel: true, PrerequisiteText: "Published prerequisite", HasPrerequisiteText: true));
        return await PublishAsync(context, tracked);
    }

    public static async Task<CourseVersion> PublishAsync(LearningDbContext context, Course course)
    {
        var version = course.Publish(new(course.DraftRevision, null,
            new(course.TenantId, Guid.CreateVersion7(), "Private teacher", course.Title, course.Description, DateTimeOffset.UtcNow)));
        context.CourseVersions.Add(version);
        var fact = PublishedCourseFact.FromVersion(version, null);
        var message = ContentOutboxMessage.Create(fact, JsonSerializer.Serialize(fact.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        message.MarkProcessed();
        context.ContentOutboxMessages.Add(message);
        await context.SaveChangesAsync(Cancellation);
        return version;
    }

    public async Task<IChannel> BindQueueAsync(IHost host)
    {
        var provider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var channel = await provider.CreateChannelAsync(Cancellation);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, true, false, cancellationToken: Cancellation);
        await channel.QueueDeclareAsync(instance, false, true, true, cancellationToken: Cancellation);
        await channel.QueueBindAsync(instance, Exchange, PublishedCourseFact.Route, cancellationToken: Cancellation);
        return channel;
    }

    public async Task<BasicGetResult> ReceiveAsync(IChannel channel)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(instance, true, Cancellation);
            if (message is not null) return message;
            await Task.Delay(100, Cancellation);
        }
        throw new TimeoutException("The published course fact did not reach the test queue.");
    }

    public Task<BasicGetResult?> TryReceiveAsync(IChannel channel) => channel.BasicGetAsync(instance, true, Cancellation);

    public async Task<List<ContentOutboxMessage>> ReplaysAsync()
    {
        await using var context = Context();
        return await context.ContentOutboxMessages.IgnoreQueryFilters().Where(item => item.MessageId != item.Id).ToListAsync(Cancellation);
    }
}
