using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
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
            where TRoot : class, IModel, IHistory =>
            Replay<TRoot>(rootId, rows, HistoryReplayOptions.Default);

        /// <summary>
        /// Same as <see cref="Replay{TRoot}(NanoId, IEnumerable{ObjectChanges})"/> but applies the supplied
        /// <paramref name="options"/> (e.g. a custom <see cref="HistoryReplayOptions.ValueConverter"/>).
        /// </summary>
        public static TRoot Replay<TRoot>(NanoId rootId, IEnumerable<ObjectChanges> rows, HistoryReplayOptions options)
            where TRoot : class, IModel, IHistory
        {
            options ??= HistoryReplayOptions.Default;
            var root = (TRoot)RuntimeHelpers.GetUninitializedObject(typeof(TRoot));
            typeof(TRoot).GetProperty(nameof(IModel.Id))!.SetValue(root, rootId);
            var any = false;
            foreach (var row in rows)
            {
                any = true;
                ApplyRow(root, row, options);
            }
            return any ? root : null;
        }

        private static void ApplyRow(object root, ObjectChanges row, HistoryReplayOptions options)
        {
            var entries = row.Select(kv => (Key: kv.Key, Value: kv.Value, Depth: kv.Key.Count(c => c == '.'))).ToList();
            var adds = entries.Where(e => e.Value.Old is null && e.Value.New is not null).OrderBy(e => e.Depth);
            var modifies = entries.Where(e => e.Value.Old is not null && e.Value.New is not null);
            var removes = entries.Where(e => e.Value.Old is not null && e.Value.New is null).OrderByDescending(e => e.Depth);
            foreach (var e in adds) ApplyEntry(root, e.Key, e.Value, options);
            foreach (var e in modifies) ApplyEntry(root, e.Key, e.Value, options);
            foreach (var e in removes) ApplyEntry(root, e.Key, e.Value, options);
        }

        private static void ApplyEntry(object root, string path, OldNewValue value, HistoryReplayOptions options)
        {
            var segments = path.Split('.');
            ApplyRecursive(root, root.GetType(), segments, 0, value, options);
        }

        private static void ApplyRecursive(object host, Type hostType, string[] segments, int index, OldNewValue value,
            HistoryReplayOptions options)
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
                        var newElement = MaterializeSnapshot(elementType, value.New, elementId, options);
                        list.Add(newElement);
                    }
                    return;
                }
                if (existing is null)
                    throw new InvalidOperationException(
                        $"History replay could not locate element '{elementId}' in collection '{segment}' on type '{hostType.FullName}'.");
                ApplyRecursive(existing, elementType, segments, index + 2, value, options);
                return;
            }

            // Non-collection navigation or scalar
            if (index == segments.Length - 1)
            {
                AssignValue(host, property, value.New, options);
                return;
            }
            var sub = property.GetValue(host);
            if (sub is null)
            {
                sub = RuntimeHelpers.GetUninitializedObject(property.PropertyType);
                property.SetValue(host, sub);
            }
            ApplyRecursive(sub, property.PropertyType, segments, index + 1, value, options);
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

        private static object MaterializeSnapshot(Type elementType, object rawSnapshot, NanoId id, HistoryReplayOptions options)
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
                    AssignValue(instance, prop, value, options);
                }
            }
            return instance;
        }

        private static void AssignValue(object host, PropertyInfo property, object rawValue, HistoryReplayOptions options)
        {
            var converted = ConvertPropertyValue(property, rawValue, options);
            if (converted is null
                && property.PropertyType.IsValueType
                && Nullable.GetUnderlyingType(property.PropertyType) is null)
            {
                // Non-nullable value-type scalar: cannot set null, fall back to default(T).
                property.SetValue(host, Activator.CreateInstance(property.PropertyType));
                return;
            }
            property.SetValue(host, converted);
        }

        private static object ConvertPropertyValue(PropertyInfo property, object rawValue, HistoryReplayOptions options)
        {
            var value = options.ValueConverter is null ? rawValue : options.ValueConverter(property, rawValue);
            return ConvertValue(value, property.PropertyType, options);
        }

        /// <summary>
        /// Converts a raw change-set value (as produced by <see cref="ObjectChanges.FromJson"/>) to
        /// <paramref name="targetType"/>. Handles <see cref="Nullable{T}"/>, <see cref="NanoId"/>, enums,
        /// <see cref="Guid"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>,
        /// <see cref="TimeOnly"/>, numbers, nested objects and collections.
        /// Timestamp strings assigned to a <see cref="DateOnly"/> become their UTC date.
        /// </summary>
        public static object ConvertValue(object value, Type targetType) =>
            ConvertValue(value, targetType, HistoryReplayOptions.Default);

        private static object ConvertValue(object value, Type targetType, HistoryReplayOptions options)
        {
            if (value is null) return null;
            var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (t.IsInstanceOfType(value)) return value;
            if (t == typeof(NanoId)) return (NanoId)Convert.ToString(value, CultureInfo.InvariantCulture);
            if (t.IsEnum)
            {
                return value switch
                {
                    string s => Enum.Parse(t, s, ignoreCase: true),
                    _ => Enum.ToObject(t, Convert.ChangeType(value, Enum.GetUnderlyingType(t), CultureInfo.InvariantCulture))
                };
            }
            if (t == typeof(Guid) && value is string gs) return Guid.Parse(gs);
            if (t == typeof(DateTime) && value is string ds) return DateTime.Parse(ds, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (t == typeof(DateTimeOffset) && value is string dos) return DateTimeOffset.Parse(dos, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (t == typeof(DateOnly)) return ToDateOnly(value);
            if (t == typeof(TimeOnly)) return ToTimeOnly(value);
            if (value is IDictionary<string, object> dict && !t.IsPrimitive && t != typeof(string))
            {
                var instance = RuntimeHelpers.GetUninitializedObject(t);
                foreach (var (k, v) in dict)
                {
                    var prop = t.GetProperty(k);
                    if (prop is null || !prop.CanWrite) continue;
                    AssignValue(instance, prop, v, options);
                }
                return instance;
            }
            if (value is object[] arr)
            {
                if (t.IsArray)
                {
                    var elem = t.GetElementType()!;
                    var array = Array.CreateInstance(elem, arr.Length);
                    for (var i = 0; i < arr.Length; i++) array.SetValue(ConvertValue(arr[i], elem, options), i);
                    return array;
                }
                if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
                {
                    var elem = t.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elem))!;
                    foreach (var item in arr) list.Add(ConvertValue(item, elem, options));
                    return list;
                }
            }
            try
            {
                return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

        private static readonly string[] _dateOnlyFormats = ["yyyy-MM-dd"];
        private static readonly string[] _timeOnlyFormats = ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"];

        private static object ToDateOnly(object value)
        {
            switch (value)
            {
                case string s:
                    if (DateOnly.TryParseExact(s, _dateOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        return date;
                    // Timestamp string (e.g. legacy DateTime value): take its UTC date.
                    return DateOnly.FromDateTime(ParseUtc(s));
                case DateTime dt:
                    return DateOnly.FromDateTime(dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt);
                case DateTimeOffset dto:
                    return DateOnly.FromDateTime(dto.UtcDateTime);
                default:
                    return value;
            }
        }

        private static object ToTimeOnly(object value)
        {
            switch (value)
            {
                case string s:
                    if (TimeOnly.TryParseExact(s, _timeOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                        return time;
                    return TimeOnly.FromDateTime(ParseUtc(s));
                case TimeSpan ts:
                    return TimeOnly.FromTimeSpan(ts);
                case DateTime dt:
                    return TimeOnly.FromDateTime(dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt);
                case DateTimeOffset dto:
                    return TimeOnly.FromDateTime(dto.UtcDateTime);
                default:
                    return value;
            }
        }

        private static DateTime ParseUtc(string s) =>
            DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

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
