using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Services;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Mapping.Mapster.Tests;

public class SourceMapTest
{
    public int Id { get; set; }
    public NestedClass Nested { get; set; } = null!;
    public required List<NestedClass> Classes { get; set; }
}

public class NestedClass
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
public class NestedClass2
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class TargetClass
{
    public int Id { get; set; }

    [MapFrom<SourceMapTest>(nameof(SourceMapTest.Classes), nameof(NestedClass.Name))]
    public IEnumerable<string> Names { get; set; } = null!;

    [MapFrom<SourceMapTest>(nameof(SourceMapTest.Classes))]
    public IEnumerable<NestedClass2> Classes { get; set; } = null!;

    public NestedClass2 Nested { get; set; } = null!;
}

public class MapsterPluginTests
{
    private MapsterPlugin _plugin;

    [SetUp]
    public void Setup()
    {
        TypeAdapterConfig.GlobalSettings.RuleMap.Clear();
        var implResolver = Substitute.For<IImplementationResolver>();
        implResolver.FindAssemblies().Returns([typeof(MapsterPluginTests).Assembly]);
        _plugin = new MapsterPlugin(implResolver);
    }

    [TearDown]
    public void Down()
    {
        TypeAdapterConfig.GlobalSettings.RuleMap.Clear();
    }

    [Test]
    public void InitsMapster()
    {
        TypeAdapterConfig.GlobalSettings.RuleMap.Should().BeEmpty();
        _plugin.ConfigureServices(new ServiceCollection());
        TypeAdapterConfig.GlobalSettings.RuleMap.Should().NotBeEmpty();
    }

    [Test]
    public void MapsCorrectly()
    {
        _plugin.ConfigureServices(new ServiceCollection());
        var src = new SourceMapTest
        {
            Id = 1,
            Classes =
            [
                new() { Name = "A", Id = 123 },
                new() { Name = "B", Id = 1234 }
            ],
            Nested = new NestedClass { Name = "C", Id = 12345 }
        };
        var target = src.Adapt<TargetClass>();
        target.Id.Should().Be(1);
        target.Names.Should().HaveCount(2);
        target.Names.Should().Contain("A");
        target.Names.Should().Contain("B");
        target.Classes.Should().HaveCount(2);
        target.Classes.Should().Contain(x => x.Name == "A" && x.Id == 123);
        target.Classes.Should().Contain(x => x.Name == "B" && x.Id == 1234);
        target.Nested.Id.Should().Be(12345);
        target.Nested.Name.Should().Be("C");
    }
}
