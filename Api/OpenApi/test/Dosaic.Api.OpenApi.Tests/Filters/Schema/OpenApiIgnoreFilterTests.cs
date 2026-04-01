using System.Text.Json.Nodes;
using AwesomeAssertions;
using Dosaic.Api.OpenApi.Filters.Common;
using Dosaic.Api.OpenApi.Schema;
using Microsoft.OpenApi;
using NUnit.Framework;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Tests.Filters.Schema
{
    public class OpenApiIgnoreFilterTests
    {
        private readonly ISchemaFilter _filter = new OpenApiIgnoreSchemaFilter();

        [Test]
        public void RemovesIgnoredPropertyFromSchema()
        {
            var openApiSchema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    { "Id", new OpenApiSchema() },
                    { "Secret", new OpenApiSchema() }
                }
            };
            var schemaContext = new SchemaFilterContext(typeof(SampleWithIgnoredProperty), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Properties.Should().ContainKey("Id");
            openApiSchema.Properties.Should().NotContainKey("Secret");
        }

        [Test]
        public void KeepsAllPropertiesWhenNoneAreIgnored()
        {
            var openApiSchema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    { "Id", new OpenApiSchema() },
                    { "Name", new OpenApiSchema() }
                }
            };
            var schemaContext = new SchemaFilterContext(typeof(SampleWithNoIgnoredProperty), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Properties.Should().ContainKey("Id");
            openApiSchema.Properties.Should().ContainKey("Name");
        }

        [Test]
        public void StripsEnumValuesForIgnoredEnumType()
        {
            var openApiSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Enum = new List<JsonNode> { JsonValue.Create(0), JsonValue.Create(1) }
            };
            var schemaContext = new SchemaFilterContext(typeof(IgnoredStatusEnum), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Enum.Should().BeEmpty();
            openApiSchema.Type.Should().Be(JsonSchemaType.Integer);
        }

        [Test]
        public void DoesNotAffectNonIgnoredEnumType()
        {
            var openApiSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Enum = new List<JsonNode> { JsonValue.Create(0), JsonValue.Create(1) }
            };
            var schemaContext = new SchemaFilterContext(typeof(NonIgnoredStatusEnum), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Enum.Should().HaveCount(2);
        }

        [Test]
        public void RemovesIgnoredEnumMembersFromSchema()
        {
            var openApiSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Enum = new List<JsonNode> { JsonValue.Create(0), JsonValue.Create(1), JsonValue.Create(2) }
            };

            var schemaContext = new SchemaFilterContext(typeof(PartiallyIgnoredStatusEnum), null, null);

            _filter.Apply(openApiSchema, schemaContext);

            openApiSchema.Enum.Should().HaveCount(2);
            openApiSchema.Enum.Should().Contain(v => v!.ToString() == "0");
            openApiSchema.Enum.Should().Contain(v => v!.ToString() == "2");
            openApiSchema.Enum.Should().NotContain(v => v!.ToString() == "1");
        }

        [Test]
        public void DoesNothingWhenSchemaHasNoProperties()
        {
            var openApiSchema = new OpenApiSchema();
            var schemaContext = new SchemaFilterContext(typeof(SampleWithIgnoredProperty), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Properties.Should().BeNull();
        }

        private class SampleWithIgnoredProperty
        {
            public Guid Id { get; set; }

            [OpenApiIgnore]
            public string Secret { get; set; }
        }

        private class SampleWithNoIgnoredProperty
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        [OpenApiIgnore]
        private enum IgnoredStatusEnum
        {
            Active = 0,
            Inactive = 1
        }

        private enum NonIgnoredStatusEnum
        {
            Active = 0,
            Inactive = 1
        }

        private enum PartiallyIgnoredStatusEnum
        {
            Active = 0,
            [OpenApiIgnore]
            Inactive = 1,
            Suspended = 2
        }
    }
}

