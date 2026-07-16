using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace ConvoLab.ArchitectureTests;

/// <summary>
/// Architecture tests for the Prompt Engine domain.
/// These tests enforce structural invariants that must hold as the codebase evolves.
/// </summary>
public class PromptEngineArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Prompt.Aggregates.Prompt).Assembly;

    [Fact]
    public void Prompt_Aggregates_Should_Inherit_From_BaseAggregateRoot()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt.Aggregates")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(Domain.Common.BaseAggregateRoot<>))
            .GetResult();

        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Prompt_Entities_Should_Inherit_From_BaseEntity()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt.Entities")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(Domain.Common.BaseEntity<>))
            .GetResult();

        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Prompt_ValueObjects_Should_Inherit_From_ValueObject()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt.ValueObjects")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(Domain.Common.ValueObject))
            .GetResult();

        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Prompt_Events_Should_Implement_IDomainEvent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt.Events")
            .And()
            .AreClasses()
            .Should()
            .ImplementInterface(typeof(Domain.Events.IDomainEvent))
            .GetResult();

        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Prompt_Domain_Should_Not_Depend_On_Application_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt")
            .ShouldNot()
            .HaveDependencyOn("ConvoLab.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, "Prompt domain types must not depend on the Application layer.");
    }

    [Fact]
    public void Prompt_Domain_Should_Not_Depend_On_Infrastructure_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ConvoLab.Domain.Prompt")
            .ShouldNot()
            .HaveDependencyOn("ConvoLab.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Prompt domain types must not depend on the Infrastructure layer.");
    }
}
