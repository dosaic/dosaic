using System.ComponentModel;
using AwesomeAssertions;
using Dosaic.Api.OpenApi.Filters.Schema;
using Microsoft.OpenApi;
using NUnit.Framework;
using Swashbuckle.AspNetCore.SwaggerGen;
// ReSharper disable UnusedMember.Local

namespace Dosaic.Api.OpenApi.Tests.Filters.Schema
{
    public class ReadOnlyPropertyFilterTests
    {
        private readonly ISchemaFilter _filter = new ReadOnlyPropertySchemaFilter();

        [Test]
        public void SetsOpenApiSchemaToReadOnly()
        {
            var openApiSchema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    {"Id", new OpenApiSchema {ReadOnly = false}},
                    {"Name", new OpenApiSchema {ReadOnly = false}}
                }
            };
            var schemaContext = new SchemaFilterContext(typeof(SampleObj), null, null);
            _filter.Apply(openApiSchema, schemaContext);
            openApiSchema.Properties["Id"].ReadOnly.Should().BeTrue();
            openApiSchema.Properties["Name"].ReadOnly.Should().BeFalse();
        }

        private class SampleObj
        {
            [ReadOnly(true)]
            public Guid Id { get; set; }

            public string Name { get; set; } = null!;
        }
    }
}
