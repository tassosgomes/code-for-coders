using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using CodeForCoders.Media.Api.Endpoints;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases;
using CodeForCoders.Media.Domain.SeedWork;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace CodeForCoders.Media.ArchitectureTests;

internal static class ProjectArchitecture
{
    private static readonly Assembly DomainAssembly = typeof(TenantId).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUseCase<,>).Assembly;
    private static readonly Assembly InfraDataAssembly = typeof(MediaDbContext).Assembly;
    private static readonly Assembly InfraMessagingAssembly = typeof(RabbitMqPublisher).Assembly;
    private static readonly Assembly ApiAssembly = typeof(PlatformEndpoints).Assembly;

    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfraDataAssembly, InfraMessagingAssembly, ApiAssembly)
        .Build();

    public static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain");
    public static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application");
    public static readonly IObjectProvider<IType> InfraLayer =
        Types().That().ResideInAssembly(InfraDataAssembly).Or().ResideInAssembly(InfraMessagingAssembly).As("Infra");
    public static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(ApiAssembly).As("Api");

    public static readonly IObjectProvider<IType> UseCases =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Application.UseCases.").As("Use cases");
    public static readonly IObjectProvider<IType> ApiExtensions =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Api.Extensions.").As("Composition root");
    public static readonly IObjectProvider<IType> Endpoints =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Api.Endpoints.").As("Endpoints");
    public static readonly IObjectProvider<IType> ApiContracts =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Api.ApiModels.").As("HTTP contracts");
    public static readonly IObjectProvider<IType> DomainEntities =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Domain.Entities.").As("Domain entities");
    public static readonly IObjectProvider<IType> PersistencePorts =
        Types().That().HaveFullNameContaining("CodeForCoders.Media.Domain.Repositories.")
            .Or().HaveFullNameContaining("CodeForCoders.Media.Application.Interfaces.IUnitOfWork")
            .Or().HaveFullNameContaining("CodeForCoders.Media.Application.Interfaces.IOutboxMessageWriter")
            .As("Persistence ports");

    public static readonly IObjectProvider<IType> EntityFrameworkCore =
        Types(true).That().HaveFullNameContaining("Microsoft.EntityFrameworkCore").As("EF Core");
    public static readonly IObjectProvider<IType> AspNetCore =
        Types(true).That().HaveFullNameContaining("Microsoft.AspNetCore").As("ASP.NET Core");
    public static readonly IObjectProvider<IType> RabbitMqClient =
        Types(true).That().HaveFullNameContaining("RabbitMQ.Client").As("RabbitMQ.Client");
    public static readonly IObjectProvider<IType> MediatR =
        Types(true).That().HaveFullNameContaining("MediatR").As("MediatR");
}
