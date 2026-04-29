using System.Reflection;
using System.Xml.Linq;
using AwesomeAssertions;
using Dosaic.Api.OpenApi.Filters.Common;
using Dosaic.Api.OpenApi.Filters.Schema;
using Microsoft.OpenApi;
using NUnit.Framework;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Tests.Filters.Schema;

internal enum TestEnum
{
    /// <summary>
    ///     Fallback.
    /// </summary>
    [OpenApiIgnore]
    Undefined = 0,

    /// <summary>
    ///     This is the real summery
    /// </summary>
    RealValue = 1,

    /// <summary>
    /// </summary>
    EmptySummary = 2,

    NoSummary = 3,

    /// <summary>
    ///     Fallback.
    /// </summary>
    [OpenApiIgnore]
    Fallback = 4
}

[TestFixture]
public class EnumSummarySchemaFilterTests
{
    private EnumSummarySchemaFilter _sut = null!;

    [SetUp]
    public void Setup()
    {
        _sut = new EnumSummarySchemaFilter();
    }

    [Test]
    public void AddsEnumMemberSummariesToDescription()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(TestEnum));

        _sut.Apply(schema, context);

        schema.Description.Should().Contain("RealValue");
        schema.Description.Should().Contain("This is the real summery");
    }

    [Test]
    public void SkipsEnumMembersWithOpenApiIgnoreAttribute()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(TestEnum));

        _sut.Apply(schema, context);

        schema.Description.Should().NotContain("Undefined");
        schema.Description.Should().NotContain("Fallback");
    }

    [Test]
    public void SkipsNonEnumTypes()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(string));

        _sut.Apply(schema, context);

        schema.Description.Should().BeNull();
    }

    [Test]
    public void PreservesExistingDescription()
    {
        var schema = new OpenApiSchema { Description = "RealValue" };
        var context = CreateSchemaFilterContext(typeof(TestEnum));

        _sut.Apply(schema, context);

        schema.Description.Should().StartWith("RealValue");
        schema.Description.Should().Contain("This is the real summery");
    }

    [Test]
    public void SkipsEnumMembersWithEmptySummary()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(TestEnum));

        _sut.Apply(schema, context);

        schema.Description.Should().Contain("RealValue");
        schema.Description.Should().NotContain("EmptySummary");
    }

    [Test]
    public void SkipsEnumMembersWithoutSummary()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(TestEnum));

        _sut.Apply(schema, context);

        schema.Description.Should().Contain("RealValue");
        schema.Description.Should().NotContain("NoSummary");
    }

    [Test]
    public void ContinuesWhenMemberFieldCannotBeResolved()
    {
        var schema = new OpenApiSchema();
        var context = CreateSchemaFilterContext(typeof(TestEnum));
        _sut = new EnumSummarySchemaFilterWithMissingRealValueField();

        _sut.Apply(schema, context);

        schema.Description.Should().BeNull();
    }

    [Test]
    public void ReturnsWhenXmlDocumentCannotBeResolved()
    {
        var schema = new OpenApiSchema { Description = "Existing" };
        var context = CreateSchemaFilterContext(typeof(TestEnum));
        _sut = new EnumSummarySchemaFilterWithMissingXmlDoc();

        _sut.Apply(schema, context);

        schema.Description.Should().Be("Existing");
    }

    private static SchemaFilterContext CreateSchemaFilterContext(Type type)
    {
        return new SchemaFilterContext(
            type,
            schemaGenerator: null!,
            schemaRepository: new SchemaRepository());
    }

    private sealed class EnumSummarySchemaFilterWithMissingRealValueField : EnumSummarySchemaFilter
    {
        protected override FieldInfo ResolveMemberField(Type type, object member)
        {
            return Equals(member, TestEnum.RealValue)
                ? null
                : base.ResolveMemberField(type, member);
        }
    }

    private sealed class EnumSummarySchemaFilterWithMissingXmlDoc : EnumSummarySchemaFilter
    {
        protected override XDocument ResolveXmlDoc()
        {
            return null;
        }
    }
}

