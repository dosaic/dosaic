using System.Net.Http;
using AwesomeAssertions;
using Dosaic.Api.OpenApi.Filters.Common;
using Dosaic.Api.OpenApi.Filters.Document;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using NUnit.Framework;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Tests.Filters.Document
{
    public class ValueObjectDocumentFilterTests
    {
        private readonly IDocumentFilter _documentFilter = new ValueObjectDocumentFilter();

        [Test]
        public void AdjustsSchemaForValueObjects()
        {
            var openApiDoc = new OpenApiDocument
            {
                Components =
                    new OpenApiComponents
                    {
                        Schemas = new Dictionary<string, IOpenApiSchema>
                        {
                            {
                                "X", new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Integer,
                                    Format = "int32",
                                    Extensions =
                                        new Dictionary<string, IOpenApiExtension>
                                        {
                                            { Markers.ValueObjectFormatMarker, null }
                                        }
                                }
                            },
                            { "int", new OpenApiSchema { Type = JsonSchemaType.Number, Format = "int32" } }
                        }
                    },
                Paths = new OpenApiPaths
                {
                    {
                        "/test", new OpenApiPathItem
                        {
                            Operations = new Dictionary<HttpMethod, OpenApiOperation>
                            {
                                {
                                    HttpMethod.Get, new OpenApiOperation
                                    {
                                        Parameters = new List<IOpenApiParameter>
                                        {
                                            new OpenApiParameter()
                                            {
                                                Name = "id2",
                                                Schema = new OpenApiSchemaReference("X"),
                                                Extensions =
                                                    new Dictionary<string, IOpenApiExtension>
                                                    {
                                                        { Markers.ValueObjectFormatMarker, null }
                                                    }
                                            }
                                        }
                                    }
                                }
                            },
                            Parameters = new List<IOpenApiParameter>
                            {
                                new OpenApiParameter()
                                {
                                    Name = "id",
                                    Schema = new OpenApiSchemaReference("X"),
                                    Extensions =
                                        new Dictionary<string, IOpenApiExtension>
                                        {
                                            { Markers.ValueObjectFormatMarker, null }
                                        }
                                },
                                new OpenApiParameter() { Name = "test" }
                            }
                        }
                    }
                }
            };
            var schemaGenerator = new SchemaGenerator(new(), new JsonSerializerDataContractResolver(new()));
            var schemaRepository = new SchemaRepository();
            var docFilterContext =
                new DocumentFilterContext(Array.Empty<ApiDescription>(), schemaGenerator, schemaRepository);
            _documentFilter.Apply(openApiDoc, docFilterContext);
            openApiDoc.Components.Schemas.Should().HaveCount(1);
            openApiDoc.Components.Schemas.Should().Contain(x => x.Key == "int");
            openApiDoc.Paths.Should().HaveCount(1);
            var path = openApiDoc.Paths.Single().Value;
            path.Parameters.Should().HaveCount(2);
            path.Parameters.Should().Contain(x => x.Name == "id" && x.Schema.Type == JsonSchemaType.Integer);
            var operation = path.Operations.Single().Value;
            operation.Parameters.Should().HaveCount(1);
            operation.Parameters.Single().Schema.Type.Should().Be(JsonSchemaType.Integer);
        }
    }
}
