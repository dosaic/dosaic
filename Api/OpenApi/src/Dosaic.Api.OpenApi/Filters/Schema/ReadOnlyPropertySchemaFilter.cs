using System.ComponentModel;
using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Filters.Schema
{
    public class ReadOnlyPropertySchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {

            var properties = context.Type.GetProperties().Where(prop => prop.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true);
            foreach (var property in properties)
            {
                var schemaId = schema.Properties.Keys.FirstOrDefault(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase));
                if (schemaId is null) continue;
                var nestedSchema = schema.Properties[schemaId];
                if (nestedSchema is OpenApiSchema openApiSchema)
                {
                    openApiSchema.ReadOnly = true;
                }
            }
        }
    }
}
