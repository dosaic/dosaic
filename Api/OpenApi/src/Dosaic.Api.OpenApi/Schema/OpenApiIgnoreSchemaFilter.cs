using System.Reflection;
using Dosaic.Api.OpenApi.Filters.Common;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Schema
{
    public class OpenApiIgnoreSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetCustomAttribute<OpenApiIgnoreAttribute>() != null)
            {
                if (schema is OpenApiSchema openApiSchema)
                {
                    openApiSchema.Enum?.Clear();
                    openApiSchema.Type = context.Type.IsEnum
                        ? (Enum.GetUnderlyingType(context.Type) == typeof(long) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(ulong) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(short) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(ushort) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(byte) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(sbyte) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(int) ||
                           Enum.GetUnderlyingType(context.Type) == typeof(uint)
                            ? JsonSchemaType.Integer
                            : JsonSchemaType.String)
                        : openApiSchema.Type;
                }

                return;
            }

            if (context.Type.IsEnum && schema.Enum is { Count: > 0 })
            {
                var ignoredEnumMembers = context.Type
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.GetCustomAttribute<OpenApiIgnoreAttribute>() != null)
                    .SelectMany(f => new[] { f.Name, f.GetRawConstantValue()?.ToString() })
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (ignoredEnumMembers.Count > 0)
                {
                    var enumValuesToRemove = schema.Enum
                        .Where(v => v?.ToString() is string value && ignoredEnumMembers.Contains(value))
                        .ToList();

                    foreach (var enumValue in enumValuesToRemove)
                        schema.Enum.Remove(enumValue);
                }
            }

            if (schema.Properties == null || schema.Properties.Count == 0) return;

            var ignoredProperties = context.Type
                .GetProperties()
                .Where(p => p.GetCustomAttribute<OpenApiIgnoreAttribute>() != null)
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ignoredProperties.Count == 0) return;

            foreach (var key in ignoredProperties)
                schema.Properties.Remove(key);
        }
    }
}

