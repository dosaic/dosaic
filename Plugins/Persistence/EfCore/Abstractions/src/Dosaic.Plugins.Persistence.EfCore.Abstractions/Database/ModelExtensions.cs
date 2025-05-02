using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class ModelExtensions
    {
        private static IEnumerable<PropertyInfo> GetPatchProperties<T>() where T : class, IModel
        {
            var props = DbModel.GetNestedProperties<T>()
                .Where(x => x.ParentProperty is null && x.Name != nameof(IModel.Id))
                .Select(x => x.Property);
            return props;
        }

        public static void Patch<T>(this T model, T values, bool ignoreLists = false) where T : class, IModel
        {
            if (model is null || values is null) return;
            foreach (var prop in GetPatchProperties<T>())
            {
                var newValue = prop.GetValue(values);
                var oldValue = prop.GetValue(model);
                if (newValue is null || newValue.Equals(oldValue))
                    continue;
                if (prop.PropertyType.IsGenericType && prop.PropertyType.Implements(typeof(IEnumerable)))
                {
                    if (ignoreLists) continue;
                    newValue = GetListValue(prop, oldValue, newValue);
                }

                prop.SetValue(model, newValue);
            }
        }

        private static object GetListValue(PropertyInfo prop, object oldValue, object newValue)
        {
            oldValue ??=
                Activator.CreateInstance(
                    typeof(Collection<>).MakeGenericType(prop.PropertyType.GenericTypeArguments[0]));
            if (oldValue is not IList list || newValue is not IList newList)
                return null;
            foreach (var newItem in newList)
                if (!list.Contains(newItem))
                    list.Add(newItem);
            return list;
        }
    }
}
