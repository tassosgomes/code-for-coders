using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using CodeForCoders.Billing.Api.Endpoints;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Application.UseCases;
using CodeForCoders.Billing.Domain.SeedWork;
using CodeForCoders.Billing.Infra.Data;
using CodeForCoders.Billing.Infra.Messaging;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace CodeForCoders.Billing.ArchitectureTests;

internal static class ProjectArchitecture
{
    private static readonly Assembly DomainAssembly = typeof(TenantId).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUseCase<,>).Assembly;
    private static readonly Assembly InfraDataAssembly = typeof(BillingDbContext).Assembly;
    private static readonly Assembly InfraMessagingAssembly = typeof(RabbitMqPublisher).Assembly;
    private static readonly Assembly InfraGatewayAssembly = typeof(CodeForCoders.Billing.Infra.Gateway.StripeGatewayAdapter).Assembly;
    private static readonly Assembly ApiAssembly = typeof(PlatformEndpoints).Assembly;

    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfraDataAssembly, InfraMessagingAssembly, InfraGatewayAssembly, ApiAssembly)
        .Build();

    public static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain");
    public static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application");
    public static readonly IObjectProvider<IType> InfraLayer =
        Types().That().ResideInAssembly(InfraDataAssembly).Or().ResideInAssembly(InfraMessagingAssembly).Or().ResideInAssembly(InfraGatewayAssembly).As("Infra");
    public static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(ApiAssembly).As("Api");

    public static readonly IObjectProvider<IType> UseCases =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Application.UseCases.").As("Use cases");
    public static readonly IObjectProvider<IType> ApiExtensions =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Api.Extensions.").As("Composition root");
    public static readonly IObjectProvider<IType> Endpoints =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Api.Endpoints.").As("Endpoints");
    public static readonly IObjectProvider<IType> ApiContracts =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Api.ApiModels.").As("HTTP contracts");
    public static readonly IObjectProvider<IType> DomainEntities =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Domain.Entities.").As("Domain entities");
    public static readonly IObjectProvider<IType> PersistencePorts =
        Types().That().HaveFullNameContaining("CodeForCoders.Billing.Domain.Repositories.")
            .Or().HaveFullNameContaining("CodeForCoders.Billing.Application.Interfaces.IUnitOfWork")
            .Or().HaveFullNameContaining("CodeForCoders.Billing.Application.Interfaces.IOutboxMessageWriter")
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
