using Dosaic.Api.OpenApi.Filters.Common;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Filters.Document
{
    public class ValueObjectDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var toRemove = swaggerDoc.Components.Schemas
                .Where(x => x.Value.Extensions != null && x.Value.Extensions.ContainsKey(Markers.ValueObjectFormatMarker))
                .Select(x => x.Key);
            foreach (var entry in toRemove)
            {
                var schema = swaggerDoc.Components.Schemas[entry];
                schema.Extensions.Remove(Markers.ValueObjectFormatMarker);

                var toEdit = swaggerDoc.Paths.SelectMany(x => x.Value.Operations)
                    .Where(x => x.Value.Parameters?.Any(p => ((OpenApiSchemaReference)p.Schema)?.Reference?.Id == entry) ?? false)
                    .SelectMany(x => x.Value.Parameters.Where(y => ((OpenApiSchemaReference)y.Schema)?.Reference?.Id == entry));
                foreach (var parameter in toEdit)
                {
                    if (parameter is OpenApiParameter schemaParameter)
                    {
                        schemaParameter.Schema = schema;
                    }

                }

                toEdit = swaggerDoc.Paths
                    .Where(x => x.Value.Parameters != null).SelectMany(x => x.Value.Parameters)
                    .Where(x => x.Extensions != null && x.Extensions.ContainsKey(Markers.ValueObjectFormatMarker));

                foreach (var parameter in toEdit)
                {
                    if (parameter is OpenApiParameter schemaParameter)
                    {
                        schemaParameter.Schema = schema;
                    }
                }

                swaggerDoc.Components.Schemas.Remove(entry);
            }
        }
    }
}
