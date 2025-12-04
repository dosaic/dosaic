using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.XPath;
using Dosaic.Api.OpenApi.Filters.Common;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Filters.Schema
{
    public class ValueObjectSchemaFilter : ISchemaFilter
    {
        private readonly XPathNavigator[] _xmlDocNavigators;

        public ValueObjectSchemaFilter(IEnumerable<string> xmlDocuments)
        {
            _xmlDocNavigators = xmlDocuments.Select(x => new XPathDocument(x).CreateNavigator()).ToArray();
        }

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(string)) return;
            var isValueObject = HasValueObjectAttribute(context.Type);
            if (isValueObject)
            {
                ApplyToValueObject(schema, context, context.Type);
                if (schema is not OpenApiSchema openApiSchema) return;
                openApiSchema.Extensions ??= new ConcurrentDictionary<string, IOpenApiExtension>();
                openApiSchema.Extensions.Add(Markers.ValueObjectFormatMarker, null);
            }
            else if (context.Type.IsClass)
                ApplyToClass(schema, context, context.Type);
        }

        private void ApplyToClass(IOpenApiSchema schema, SchemaFilterContext context, Type type)
        {
            var properties = from prop in type.GetProperties()
                where HasValueObjectAttribute(prop)
                      || (prop.PropertyType.Implements<IEnumerable>()
                          && prop.PropertyType.IsGenericType
                          && HasValueObjectAttribute(prop.PropertyType.GetGenericArguments()[0]))
                select prop;
            foreach (var prop in properties)
            {
                var key = schema.Properties.Keys.Single(x =>
                    string.Equals(x, prop.Name, StringComparison.InvariantCultureIgnoreCase));
                var containingType = prop.PropertyType.IsGenericType
                    ? prop.PropertyType.GetGenericArguments()[0]
                    : prop.PropertyType;
                ApplyToValueObject(schema.Properties[key], context, containingType, prop);
            }
        }

        private void ApplyToValueObject(IOpenApiSchema schema, SchemaFilterContext context, Type type,
            PropertyInfo propertyInfo = null)
        {
            var underlyingType = GetUnderlyingType(type);
            var openApiSchema = context.SchemaGenerator.GenerateSchema(underlyingType, context.SchemaRepository);
            SetSchema(schema.Type == JsonSchemaType.Array ? schema.Items : schema, openApiSchema, propertyInfo);
        }

        private void SetSchema(IOpenApiSchema source, IOpenApiSchema toOverride, PropertyInfo propertyInfo = null)
        {
            if (source is OpenApiSchema schema)
            {
                schema.Type = toOverride.Type;
                schema.Format = toOverride.Format;
                schema.AdditionalPropertiesAllowed = true;
                schema.AdditionalProperties = null;
                schema.Properties = null;

                schema.Description = propertyInfo == null ? schema.Description : GetDescription(propertyInfo);
            }



        }

        private string GetDescription(MemberInfo memberInfo)
        {
            var memberName = XmlCommentsNodeNameHelper.GetMemberNameForFieldOrProperty(memberInfo);
            var description = _xmlDocNavigators
                .Select(x => x.SelectSingleNode($"/doc/members/member[@name='{memberName}']/summary"))
                .FirstOrDefault(x => x != null);
            return description == null ? null : XmlCommentsTextHelper.Humanize(description.InnerXml);
        }

        private static bool HasValueObjectAttribute(MemberInfo type)
        {
            return GetRealType(type).GetCustomAttributes().FirstOrDefault(attr =>
                attr.ToString() != null && attr.ToString()!.Contains("Vogen.ValueObject")) != null;
        }

        private static Type GetRealType(MemberInfo memberInfo)
        {
            if (memberInfo is PropertyInfo propInfo)
                memberInfo = propInfo.PropertyType;
            return memberInfo as Type;
        }

        private static Type GetUnderlyingType(Type type)
        {
            return GetRealType(type).GetProperty("Value")!.PropertyType;
        }
    }
}
