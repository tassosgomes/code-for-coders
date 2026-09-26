using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class MediaIntegrationFixture : IAsyncLifetime
{
    public const string MinioAccessKey = "media-integration";

    public const string MinioSecretKey = "media-integration-password";

    public const string MinioBucketName = "media-integration";

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_media")
        .WithUsername("code_for_coders_media")
        .WithPassword("code_for_coders_media")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public IContainer Minio { get; } = new ContainerBuilder("ghcr.io/coollabsio/minio:RELEASE.2025-10-15T17-29-55Z")
        .WithCommand("server", "/data", "--console-address", ":9001")
        .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
        .WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9000).ForPath("/minio/health/ready")))
        .Build();

    public string MinioEndpoint => $"http://{Minio.Hostname}:{Minio.GetMappedPublicPort(9000)}";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync(), Minio.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new MediaDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }

        using var minioClient = new AmazonS3Client(
            new BasicAWSCredentials(MinioAccessKey, MinioSecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                ServiceURL = MinioEndpoint,
            });
        await minioClient.PutBucketAsync(new PutBucketRequest { BucketName = MinioBucketName });
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await Minio.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MediaIntegrationCollection : ICollectionFixture<MediaIntegrationFixture>
{
    public const string Name = "media-integration";
}
