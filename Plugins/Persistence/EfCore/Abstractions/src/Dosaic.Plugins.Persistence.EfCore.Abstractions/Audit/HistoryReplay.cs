using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Reconstructs a history root entity by replaying an ordered sequence of <see cref="ObjectChanges"/> rows.
    /// </summary>
    public static class HistoryReplay
    {
        /// <summary>
        /// Replays the supplied bundled change rows (in chronological order) against a fresh
        /// <typeparamref name="TRoot"/> instance with <see cref="IModel.Id"/> set to <paramref name="rootId"/>.
        /// Returns <c>null</c> when the resulting graph contains no Added evidence (i.e. the root never existed yet).
        /// </summary>
        public static TRoot Replay<TRoot>(NanoId rootId, IEnumerable<ObjectChanges> rows)
            where TRoot : class, IModel, IHistory
        {
            var root = (TRoot)RuntimeHelpers.GetUninitializedObject(typeof(TRoot));
            typeof(TRoot).GetProperty(nameof(IModel.Id))!.SetValue(root, rootId);
            var any = false;
            foreach (var row in rows)
            {
                any = true;
                ApplyRow(root, row);
            }
            return any ? root : null;
        }

        private static void ApplyRow(object root, ObjectChanges row)
        {
            var entries = row.Select(kv => (Key: kv.Key, Value: kv.Value, Depth: kv.Key.Count(c => c == '.'))).ToList();
            var adds = entries.Where(e => e.Value.Old is null && e.Value.New is not null).OrderBy(e => e.Depth);
            var modifies = entries.Where(e => e.Value.Old is not null && e.Value.New is not null);
            var removes = entries.Where(e => e.Value.Old is not null && e.Value.New is null).OrderByDescending(e => e.Depth);
            foreach (var e in adds) ApplyEntry(root, e.Key, e.Value);
            foreach (var e in modifies) ApplyEntry(root, e.Key, e.Value);
            foreach (var e in removes) ApplyEntry(root, e.Key, e.Value);
        }

        private static void ApplyEntry(object root, string path, OldNewValue value)
        {
            var segments = path.Split('.');
            ApplyRecursive(root, root.GetType(), segments, 0, value);
        }

        private static void ApplyRecursive(object host, Type hostType, string[] segments, int index, OldNewValue value)
        {
            var segment = segments[index];
            var property = hostType.GetProperty(segment);
            if (property is null)
                throw new InvalidOperationException(
                    $"History replay could not find property '{segment}' on type '{hostType.FullName}' for path '{string.Join('.', segments)}'.");

            var isCollection = IsEntityCollection(property.PropertyType);
            if (isCollection)
            {
                var elementType = GetCollectionElementType(property.PropertyType);
                var list = EnsureList(host, property);
                if (index + 1 >= segments.Length)
                    throw new InvalidOperationException(
                        $"History replay path '{string.Join('.', segments)}' ends on collection '{segment}' without an id.");
                var elementIdRaw = segments[index + 1];
                NanoId elementId = elementIdRaw;
                var existing = FindById(list, elementId);
                if (index + 2 == segments.Length)
                {
                    // Terminal element add / remove (value is full snapshot or null)
                    if (value.New is null)
                    {
                        if (existing != null) list.Remove(existing);
                    }
                    else
                    {
                        if (existing != null) list.Remove(existing);
                        var newElement = MaterializeSnapshot(elementType, value.New, elementId);
                        list.Add(newElement);
                    }
                    return;
                }
                if (existing is null)
                    throw new InvalidOperationException(
                        $"History replay could not locate element '{elementId}' in collection '{segment}' on type '{hostType.FullName}'.");
                ApplyRecursive(existing, elementType, segments, index + 2, value);
                return;
            }

            // Non-collection navigation or scalar
            if (index == segments.Length - 1)
            {
                property.SetValue(host, CoerceValue(value.New, property.PropertyType));
                return;
            }
            var sub = property.GetValue(host);
            if (sub is null)
            {
                sub = RuntimeHelpers.GetUninitializedObject(property.PropertyType);
                property.SetValue(host, sub);
            }
            ApplyRecursive(sub, property.PropertyType, segments, index + 1, value);
        }

        private static readonly ConcurrentDictionary<Type, PropertyInfo> _idPropertyCache = new();

        private static object FindById(IList list, NanoId id)
        {
            foreach (var item in list)
            {
                if (item is null) continue;
                var idProp = _idPropertyCache.GetOrAdd(item.GetType(),
                    t => t.GetProperty(nameof(IModel.Id))!);
                var v = (NanoId)idProp.GetValue(item)!;
                if (v.Equals(id)) return item;
            }
            return null;
        }

        private static IList EnsureList(object host, PropertyInfo property)
        {
            var existing = property.GetValue(host);
            if (existing is IList list) return list;
            var elementType = GetCollectionElementType(property.PropertyType);
            var concrete = typeof(List<>).MakeGenericType(elementType);
            var instance = (IList)Activator.CreateInstance(concrete)!;
            property.SetValue(host, instance);
            return instance;
        }

        private static object MaterializeSnapshot(Type elementType, object rawSnapshot, NanoId id)
        {
            var instance = RuntimeHelpers.GetUninitializedObject(elementType);
            var idProp = elementType.GetProperty(nameof(IModel.Id))!;
            idProp.SetValue(instance, (object)id);
            if (rawSnapshot is IDictionary<string, object> dict)
            {
                foreach (var (key, value) in dict)
                {
                    if (key == nameof(IModel.Id)) continue;
                    var prop = elementType.GetProperty(key);
                    if (prop is null || !prop.CanWrite) continue;
                    prop.SetValue(instance, CoerceValue(value, prop.PropertyType));
                }
            }
            return instance;
        }

        private static object CoerceValue(object value, Type targetType)
        {
            if (value is null) return null;
            var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (t.IsInstanceOfType(value)) return value;
            if (t == typeof(NanoId)) return (NanoId)Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (t.IsEnum)
            {
                return value switch
                {
                    string s => Enum.Parse(t, s, ignoreCase: true),
                    _ => Enum.ToObject(t, Convert.ChangeType(value, Enum.GetUnderlyingType(t), System.Globalization.CultureInfo.InvariantCulture))
                };
            }
            if (t == typeof(Guid) && value is string gs) return Guid.Parse(gs);
            if (t == typeof(DateTime) && value is string ds) return DateTime.Parse(ds, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            if (t == typeof(DateTimeOffset) && value is string dos) return DateTimeOffset.Parse(dos, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            if (value is IDictionary<string, object> dict && !t.IsPrimitive && t != typeof(string))
            {
                var instance = RuntimeHelpers.GetUninitializedObject(t);
                foreach (var (k, v) in dict)
                {
                    var prop = t.GetProperty(k);
                    if (prop is null || !prop.CanWrite) continue;
                    prop.SetValue(instance, CoerceValue(v, prop.PropertyType));
                }
                return instance;
            }
            if (value is object[] arr)
            {
                if (t.IsArray)
                {
                    var elem = t.GetElementType()!;
                    var array = Array.CreateInstance(elem, arr.Length);
                    for (var i = 0; i < arr.Length; i++) array.SetValue(CoerceValue(arr[i], elem), i);
                    return array;
                }
                if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
                {
                    var elem = t.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elem))!;
                    foreach (var item in arr) list.Add(CoerceValue(item, elem));
                    return list;
                }
            }
            try
            {
                return Convert.ChangeType(value, t, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

        private static bool IsEntityCollection(Type t)
        {
            if (t == typeof(string)) return false;
            if (!typeof(IEnumerable).IsAssignableFrom(t)) return false;
            var elem = GetCollectionElementType(t);
            return elem is not null && typeof(IModel).IsAssignableFrom(elem);
        }

        private static Type GetCollectionElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType) return t.GetGenericArguments()[0];
            var ienum = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return ienum?.GetGenericArguments()[0];
        }
    }
}
