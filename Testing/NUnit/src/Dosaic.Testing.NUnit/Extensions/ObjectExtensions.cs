using System.Reflection;

namespace Dosaic.Testing.NUnit.Extensions
{
    public static class ObjectExtensions
    {
        public static T GetInaccessibleValue<T>(this object obj, string name)
        {
            var type = obj?.GetType() ?? throw new ArgumentNullException(nameof(obj));
            while (type is not null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                if (field is not null)
                    return (T)field.GetValue(obj)!;
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                if (property is not null)
                    return (T)property.GetValue(obj)!;
                type = type.BaseType;
            }
            throw new ArgumentException($"Could not find field or property '{name}'");
        }

        public static object GetInaccessibleValue(this object obj, string name) =>
            obj.GetInaccessibleValue<object>(name);
    }
}
