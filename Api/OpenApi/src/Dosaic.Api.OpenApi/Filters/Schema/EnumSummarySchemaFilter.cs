using System.Collections.Concurrent;
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
public class EnumSummarySchemaFilter : ISchemaFilter
{
    private static readonly ConcurrentDictionary<string, Lazy<XDocument>> _xmlDocCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string[] _documentationFiles;

    public EnumSummarySchemaFilter() : this(null) { }

    public EnumSummarySchemaFilter(string[] documentationFiles)
    {
        _documentationFiles = documentationFiles ?? Array.Empty<string>();
    }

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum) return;

        var docs = ResolveXmlDocs(enumType);
        if (docs.Count == 0) return;

        var entries = new List<string>();
        foreach (var member in Enum.GetValues(enumType))
        {
            var field = ResolveMemberField(enumType, member);
            if (field is null) continue;
            if (field.GetCustomAttribute<OpenApiIgnoreAttribute>() is not null) continue;

            var summary = GetXmlSummary(docs, field);
            if (string.IsNullOrWhiteSpace(summary)) continue;

            var value = Convert.ToInt64(member, CultureInfo.InvariantCulture);
            entries.Add($"- `{field.Name}` ({value}): {summary}");
        }

        if (entries.Count == 0) return;

        var description = string.Join("\n", entries);
        schema.Description = string.IsNullOrWhiteSpace(schema.Description)
            ? description
            : $"{schema.Description}\n\n{description}";
    }

    protected virtual FieldInfo ResolveMemberField(Type type, object member)
    {
        var name = Enum.GetName(type, member);
        return name is null ? null : type.GetField(name);
    }

    protected virtual IReadOnlyCollection<XDocument> ResolveXmlDocs(Type enumType)
    {
        var paths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, $"{enumType.Assembly.GetName().Name}.xml")
        };
        paths.AddRange(_documentationFiles);

        return paths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(LoadXmlDoc)
            .Where(doc => doc is not null)
            .ToList();
    }

    private static string GetXmlSummary(IReadOnlyCollection<XDocument> docs, FieldInfo field)
    {
        var declaringName = field.DeclaringType!.FullName!;
        var memberName = $"F:{declaringName}.{field.Name}";
        var nestedMemberName = $"F:{declaringName.Replace('+', '.')}.{field.Name}";

        foreach (var doc in docs)
        {
            var element = doc.Descendants("member").FirstOrDefault(m =>
            {
                var name = m.Attribute("name")?.Value;
                return name == memberName || name == nestedMemberName;
            });

            var summary = element?.Element("summary")?.Value.Trim();
            if (!string.IsNullOrWhiteSpace(summary)) return summary;
        }

        return null;
    }

    private static XDocument LoadXmlDoc(string xmlFile) =>
        _xmlDocCache.GetOrAdd(xmlFile, static path => new Lazy<XDocument>(() => XDocument.Load(path))).Value;
}

