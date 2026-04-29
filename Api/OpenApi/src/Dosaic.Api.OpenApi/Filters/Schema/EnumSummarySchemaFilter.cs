using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Dosaic.Api.OpenApi.Filters.Common;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dosaic.Api.OpenApi.Filters.Schema;

/// <summary>
///     Adds XML summary comments from enum members to the OpenAPI schema description.
/// </summary>
internal class EnumSummarySchemaFilter : ISchemaFilter
{
    private static readonly Lazy<XDocument> _xmlDoc = new(LoadXmlDoc);

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        if (!type.IsEnum)
            return;

        var doc = ResolveXmlDoc();
        if (doc is null)
            return;

        var entries = new List<string>();
        foreach (var member in Enum.GetValues(type))
        {
            var memberInfo = ResolveMemberField(type, member);
            if (memberInfo is null)
                continue;

            if (memberInfo.GetCustomAttribute<OpenApiIgnoreAttribute>() is not null)
                continue;

            var summary = GetXmlSummary(doc, memberInfo);
            if (string.IsNullOrWhiteSpace(summary))
                continue;

            var value = Convert.ToInt64(member, CultureInfo.InvariantCulture);
            entries.Add($"- `{memberInfo.Name}` ({value}): {summary}");
        }

        if (entries.Count > 0)
        {
            var description = string.Join("\n", entries);
            schema.Description = string.IsNullOrWhiteSpace(schema.Description)
                ? description
                : $"{schema.Description}\n\n{description}";
        }
    }

    protected virtual FieldInfo ResolveMemberField(Type type, object member)
    {
        var memberName = Enum.GetName(type, member);
        return memberName is null ? null : type.GetField(memberName);
    }

    protected virtual XDocument ResolveXmlDoc()
    {
        return _xmlDoc.Value;
    }

    private static string GetXmlSummary(XDocument doc, FieldInfo field)
    {
        var memberName = $"F:{field.DeclaringType!.FullName}.{field.Name}";
        var memberElement = doc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

        return memberElement?.Element("summary")?.Value.Trim();
    }

    private static XDocument LoadXmlDoc()
    {
        var assembly = typeof(EnumSummarySchemaFilter).Assembly;
        var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        return File.Exists(xmlFile) ? XDocument.Load(xmlFile) : null;
    }
}

