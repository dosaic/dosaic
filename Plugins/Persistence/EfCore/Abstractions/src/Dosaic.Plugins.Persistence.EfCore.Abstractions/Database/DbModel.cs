using System.Reflection;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public record ModelProperty(string Name, PropertyInfo Property, PropertyInfo ParentProperty);
    public static class DbModel
    {
        public static IList<ModelProperty> GetNestedProperties(Type model) => GetNestedModelProperties(model)[model];
        public static IList<ModelProperty> GetNestedProperties<T>() => GetNestedProperties(typeof(T));
        public static PropertyInfo[] GetProperties(Type model) => GetModelProperties(model)[model];
        public static PropertyInfo[] GetProperties<T>() => GetProperties(typeof(T));
        public static Type GetModelByName(Type dbContextType, string modelName) => GetModels(dbContextType)[modelName.ToLowerInvariant()];

        public static IDictionary<string, Type> GetModels(Type dbContextType) => dbContextType.GetAssemblyTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false, IsGenericType: false } && x.Implements<IModel>())
            .ToDictionary(x => x.Name.ToLowerInvariant(), x => x);

        private static IDictionary<Type, IList<ModelProperty>> GetNestedModelProperties(Type dbContextType) =>
            GetModels(dbContextType).ToDictionary(x => x.Value, x => GetModelPropertiesForType(x.Value));

        private static IDictionary<Type, PropertyInfo[]> GetModelProperties(Type dbContextType) =>
            GetModels(dbContextType).ToDictionary(x => x.Value,
                x => x.Value.GetProperties().Where(b => b is { CanWrite: true, CanRead: true }).ToArray());

        private static IList<ModelProperty> GetModelPropertiesForType(Type t)
        {
            var list = new List<ModelProperty>();
            AddProperties(t, null);
            return list;

            void AddProperties(Type type, PropertyInfo parentProp, string prefix = "")
            {
                var props = type.GetProperties()
                    .Where(x => x.CanWrite && !x.PropertyType.IsAssignableTo(typeof(IModel)));
                foreach (var prop in props)
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    if (!prop.PropertyType.IsClass || prop.PropertyType == typeof(string) ||
                        prop.PropertyType == typeof(NanoId))
                    {
                        list.Add(new ModelProperty(key, prop, parentProp));
                    }
                    else
                    {
                        AddProperties(prop.PropertyType, prop, key);
                    }
                }
            }
        }
    }
}
