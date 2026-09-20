using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using CodeForCoders.Identity.Api.Endpoints;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Domain.SeedWork;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Messaging;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace CodeForCoders.Identity.ArchitectureTests;

internal static class ProjectArchitecture
{
    private static readonly Assembly DomainAssembly = typeof(TenantId).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUseCase<,>).Assembly;
    private static readonly Assembly InfraDataAssembly = typeof(IdentityDbContext).Assembly;
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
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Application.UseCases.").As("Use cases");
    public static readonly IObjectProvider<IType> ApiExtensions =
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Api.Extensions.").As("Composition root");
    public static readonly IObjectProvider<IType> Endpoints =
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Api.Endpoints.").As("Endpoints");
    public static readonly IObjectProvider<IType> ApiContracts =
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Api.ApiModels.").As("HTTP contracts");
    public static readonly IObjectProvider<IType> DomainEntities =
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Domain.Entities.").As("Domain entities");
    public static readonly IObjectProvider<IType> PersistencePorts =
        Types().That().HaveFullNameContaining("CodeForCoders.Identity.Domain.Repositories.")
            .Or().HaveFullNameContaining("CodeForCoders.Identity.Application.Interfaces.IUnitOfWork")
            .Or().HaveFullNameContaining("CodeForCoders.Identity.Application.Interfaces.IOutboxMessageWriter")
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
