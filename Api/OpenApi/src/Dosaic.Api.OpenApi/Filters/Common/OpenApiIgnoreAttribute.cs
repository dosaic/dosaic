namespace Dosaic.Api.OpenApi.Filters.Common
{
    /// <summary>
    /// Marks a type, property, or enum member to be omitted from the OpenAPI schema.
    /// When applied to a property, the property is removed from the schema.
    /// When applied to an enum, the enum values are stripped and the type is represented as its underlying primitive.
    /// When applied to an enum member, only that member is removed from the enum schema.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field)]
    public sealed class OpenApiIgnoreAttribute : Attribute
    {
    }
}

