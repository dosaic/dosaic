using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    [Flags]
    public enum PatchMode
    {
        /// <summary>
        ///
        /// </summary>
        Full = 1,
        IgnoreLists = 2,
        IgnoreObjects = 4,
        OverwriteLists = 8
    }

    public static class ObjectExtensions
    {
        /// <summary>
        /// Patches non-null properties from <paramref name="patch"/> into <paramref name="value"/>.
        /// If a property is a list, new items will be added to the existing list or overriden (see PatchMode>.
        /// </summary>
        /// <param name="value">Value to be patched</param>
        /// <param name="patch">The patch to apply</param>
        /// <param name="mode">The mode (default: full)</param>
        /// <param name="filter">Optional filter for type and properties</param>
        /// <typeparam name="T"></typeparam>
        public static void DeepPatch<T>(this T value, T patch, PatchMode mode = PatchMode.Full, Func<Type, PropertyInfo, bool> filter = null) where T : class
        {
            DeepPatchInternal(value, patch, mode,
                (t, p) => p.CanRead && p.CanWrite && (filter == null || filter(t, p)));
        }

        private static object DeepPatchInternal(object value, object patch, PatchMode mode, Func<Type, PropertyInfo, bool> filter)
        {
            if (value is null || patch is null) return value ?? patch;
            var ignoreLists = mode.IsFlagSet(PatchMode.IgnoreLists);
            var ignoreObjects = mode.IsFlagSet(PatchMode.IgnoreObjects);
            var overwriteLists = mode.IsFlagSet(PatchMode.OverwriteLists);
            var type = value.GetType();
            if (type.IsEnumerable())
            {
                return ignoreLists ? value : GetListValue(type.GenericTypeArguments[0], value, patch);
            }
            var props = type.GetProperties().Where(x => filter(type, x));
            foreach (var prop in props)
            {
                var newValue = prop.GetValue(patch);
                var oldValue = prop.GetValue(value);
                if (newValue is null || newValue.Equals(oldValue))
                    continue;
                var isObject = prop.PropertyType.IsClass
                               && prop.PropertyType != typeof(string)
                               && prop.PropertyType.GetProperties().Any(x => filter(type, x));
                if (prop.PropertyType.IsEnumerable())
                {
                    if (ignoreLists) continue;
                    if (!overwriteLists)
                    {
                        newValue = GetListValue(prop.PropertyType.GenericTypeArguments[0], oldValue, newValue);
                    }
                }
                else if (isObject)
                {
                    if (ignoreObjects) continue;
                    newValue = DeepPatchInternal(oldValue, newValue, mode, filter);
                }
                prop.SetValue(value, newValue);
            }
            return value;
        }

        private static object GetListValue(Type innerType, object oldValue, object newValue)
        {
            oldValue ??=
                Activator.CreateInstance(
                    typeof(Collection<>).MakeGenericType(innerType));

            if (oldValue is not IList list || newValue is not IList newList) return null;

            foreach (var newItem in newList)
                if (!list.Contains(newItem))
                    list.Add(newItem);
            return list;
        }
    }
}
