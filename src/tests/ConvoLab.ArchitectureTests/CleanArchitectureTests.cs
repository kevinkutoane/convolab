using NetArchTest.Rules;
using Xunit;
using System.Reflection;

namespace ConvoLab.ArchitectureTests;

public class CleanArchitectureTests
{
    private const string DomainNamespace = "ConvoLab.Domain";
    private const string ApplicationNamespace = "ConvoLab.Application";
    private const string InfrastructureNamespace = "ConvoLab.Infrastructure";
    private const string ApiNamespace = "ConvoLab.Api";

    private static readonly Assembly DomainAssembly = typeof(Domain.Common.BaseEntity<>).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Other_Projects()
    {
        var otherProjects = new[]
        {
            ApplicationNamespace,
            InfrastructureNamespace,
            ApiNamespace
        };

        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_Should_Not_Have_Dependency_On_Infrastructure_And_Api()
    {
        var otherProjects = new[]
        {
            InfrastructureNamespace,
            ApiNamespace
        };

        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_Should_Not_Have_Dependency_On_Api()
    {
        var otherProjects = new[]
        {
            ApiNamespace
        };

        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Execution_Interfaces_Should_Be_In_Domain_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Execution.Interfaces")
            .Should()
            .BeInterfaces()
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_Entities_Should_Inherit_From_BaseEntity()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespaceContaining("Entities")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(Domain.Common.BaseEntity<>))
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_Events_Should_Implement_IDomainEvent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespaceContaining("Events")
            .And()
            .AreClasses()
            .Should()
            .ImplementInterface(typeof(Domain.Events.IDomainEvent))
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ValueObjects_Should_Inherit_From_ValueObject()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespaceContaining("ValueObjects")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveName("ProviderHealth")
            .And()
            .DoNotHaveName("ModelCapability")
            .And()
            .DoNotHaveName("ModelAvailability")
            .Should()
            .Inherit(typeof(Domain.Common.ValueObject))
            .GetResult();

        if (!result.IsSuccessful)
        {
            foreach (var failingType in result.FailingTypes)
            {
                Console.WriteLine($"Failing type for ValueObjects_Should_Inherit_From_ValueObject: {failingType.FullName}");
            }
        }
        Assert.True(result.IsSuccessful);
    }
}
