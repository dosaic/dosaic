using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Filters.Operation
{
    public class FormFileFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var formFileParams = context.MethodInfo.GetParameters()
                .Where(x => x.ParameterType == typeof(IFormFile))
                .ToList();
            if (!formFileParams.Any()) return;

            var mediaType = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties =
                        formFileParams.ToDictionary<ParameterInfo, string, IOpenApiSchema>(x => x.Name,
                            x => new OpenApiSchema
                            {
                                Description = "Upload File",
                                Type = JsonSchemaType.Object,
                                Format = "binary"
                            }),
                    Required = formFileParams.Select(x => x.Name).ToHashSet()
                }
            };
            var iOpenApiRequestBody = new OpenApiRequestBody
            {
                Content = new ConcurrentDictionary<string, OpenApiMediaType>()
                {
                    ["multipart/form-data"] = mediaType
                }
            };
            operation.RequestBody = iOpenApiRequestBody;
        }
    }
}
