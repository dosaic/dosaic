using System.Collections;

namespace Dosaic.Plugins.Validations.Abstractions;

public sealed record ValidationContext(object Value, string Path, IServiceProvider Services)
{
    public Type ValueType => Value?.GetType()!;
    public bool IsNullValue => Value is null;
    public bool IsObjectType => !IsNullValue && ValueType.IsClass && ValueType != typeof(string) && !IsListType && !ValueType.IsAssignableTo(typeof(IDictionary));
    public bool IsListType => !IsNullValue && ValueType.IsAssignableTo(typeof(IEnumerable)) && ValueType != typeof(string);
}
