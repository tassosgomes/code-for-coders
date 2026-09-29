using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        .WithPortBinding(15672, true)
        .Build();

    public IContainer Minio { get; } = new ContainerBuilder("ghcr.io/coollabsio/minio:RELEASE.2025-10-15T17-29-55Z")
        .WithCommand("server", "/data", "--console-address", ":9001")
        .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
        .WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9000).ForPath("/minio/health/ready")))
        .Build();

    public string MinioEndpoint => $"http://{Minio.Hostname}:{Minio.GetMappedPublicPort(9000)}";

    public string VideoPreparationConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync(), Minio.StartAsync());

        var adminConnectionString = new NpgsqlConnectionStringBuilder(PostgreSql.GetConnectionString())
        {
            Database = "postgres",
        };
        await using (var adminConnection = new NpgsqlConnection(adminConnectionString.ConnectionString))
        {
            await adminConnection.OpenAsync();
            await using var createDatabase = adminConnection.CreateCommand();
            createDatabase.CommandText = "CREATE DATABASE code_for_coders_media_preparation";
            await createDatabase.ExecuteNonQueryAsync();
        }

        VideoPreparationConnectionString = new NpgsqlConnectionStringBuilder(PostgreSql.GetConnectionString())
        {
            Database = "code_for_coders_media_preparation",
        }.ConnectionString;

        await MigrateDatabaseAsync(PostgreSql.GetConnectionString());
        await MigrateDatabaseAsync(VideoPreparationConnectionString);

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

    private static async Task MigrateDatabaseAsync(string connectionString)
    {
        var dbOptions = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                "media_access"))
            .Options;
        await using var dbContext = new MediaDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MediaIntegrationCollection : ICollectionFixture<MediaIntegrationFixture>
{
    public const string Name = "media-integration";
}
