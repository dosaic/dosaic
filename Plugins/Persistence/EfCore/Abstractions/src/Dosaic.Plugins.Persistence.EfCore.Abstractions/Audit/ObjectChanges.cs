using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Dosaic.Extensions.NanoIds;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public class ObjectChanges : Dictionary<string, OldNewValue>
    {
        public ObjectChanges FilterKeys(Predicate<string> keyFilter)
        {
            var changes = new ObjectChanges();
            foreach (var (k, v) in this.Where(x => keyFilter(x.Key)))
            {
                if (keyFilter(k))
                    changes.Add(k, v);
            }

            return changes;
        }

        public string ToJson() => this.Serialize();

        /// <summary>
        /// Merges another <see cref="ObjectChanges"/> into this instance. Duplicate keys throw.
        /// </summary>
        public ObjectChanges MergeFrom(ObjectChanges other)
        {
            if (other is null) return this;
            foreach (var (key, value) in other)
            {
                if (ContainsKey(key))
                    throw new InvalidOperationException($"Duplicate change-set key '{key}'.");
                Add(key, value);
            }
            return this;
        }

        public static ObjectChanges Calculate<T>(ChangeState state, T old, T @new) where T : class, IModel
        {
            var changes = new ObjectChanges();
            switch (state)
            {
                case ChangeState.Added:
                    WriteAddChanges(@new, changes);
                    break;
                case ChangeState.Modified:
                    WriteUpdateChanges(old, @new, changes);
                    break;
                case ChangeState.Deleted:
                    WriteDeleteChanges(old, changes);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            return changes;
        }

        /// <summary>
        /// Builds the change entries for a child entity that lives under a root history stream.
        /// For Added / Deleted, emits a single entry at <paramref name="pathPrefix"/> carrying a snapshot dictionary.
        /// For Modified, emits per-property entries keyed as <c>{pathPrefix}.{PropertyName}</c>.
        /// </summary>
        public static ObjectChanges CalculateChild<T>(ChangeState state, T old, T @new, string pathPrefix,
            params string[] excludedProperties) where T : class, IModel
        {
            var changes = new ObjectChanges();
            var skip = new HashSet<string>(excludedProperties, StringComparer.OrdinalIgnoreCase);
            switch (state)
            {
                case ChangeState.Added:
                    {
                        var snapshot = ToSnapshot(@new, skip);
                        changes.Add(pathPrefix, new OldNewValue { New = snapshot });
                        break;
                    }
                case ChangeState.Deleted:
                    {
                        var snapshot = ToSnapshot(old, skip);
                        changes.Add(pathPrefix, new OldNewValue { Old = snapshot });
                        break;
                    }
                case ChangeState.Modified:
                    {
                        foreach (var property in GetChangeTrackedProperties<T>())
                        {
                            if (skip.Contains(property.Name)) continue;
                            var newValue = property.GetValue(@new);
                            var oldValue = property.GetValue(old);
                            if (newValue is null && oldValue is null) continue;
                            if (newValue is null || oldValue is null || !newValue.Equals(oldValue))
                                changes.Add($"{pathPrefix}.{property.Name}",
                                    new OldNewValue { Old = oldValue, New = newValue });
                        }
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
            return changes;
        }

        private static Dictionary<string, object> ToSnapshot<T>(T entity, HashSet<string> skip) where T : class, IModel
        {
            var dict = new Dictionary<string, object>();
            foreach (var property in GetSnapshotProperties<T>())
            {
                if (skip.Contains(property.Name)) continue;
                var value = property.GetValue(entity);
                if (value is null) continue;
                dict[property.Name] = value;
            }
            return dict;
        }

        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _snapshotPropertyCache = new();

        private static PropertyInfo[] GetChangeTrackedProperties<T>() =>
            _propertyCache.GetOrAdd(typeof(T), type => type.GetProperties()
                .Where(x => x.CanRead && x.Name != nameof(IModel.Id))
                .Where(x => !IsEntityNavigation(x.PropertyType))
                .ToArray());

        private static PropertyInfo[] GetSnapshotProperties<T>() =>
            _snapshotPropertyCache.GetOrAdd(typeof(T), type => type.GetProperties()
                .Where(x => x is { CanRead: true, CanWrite: true })
                .Where(x => x.GetCustomAttribute<ExcludeFromHistoryAttribute>() is null)
                .Where(x => !IsEntityNavigation(x.PropertyType))
                .ToArray());

        private static bool IsEntityNavigation(Type t)
        {
            if (typeof(IModel).IsAssignableFrom(t)) return true;
            if (t == typeof(string)) return false;
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(t)) return false;
            var elem = t.IsArray
                ? t.GetElementType()
                : t.IsGenericType
                    ? t.GetGenericArguments()[0]
                    : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
            return elem is not null && typeof(IModel).IsAssignableFrom(elem);
        }

        private static void WriteDeleteChanges<T>(T old, ObjectChanges changes) where T : class, IModel
        {
            foreach (var property in GetChangeTrackedProperties<T>())
            {
                var value = property.GetValue(old);
                if (value is not null)
                    changes.Add(property.Name, new OldNewValue { Old = value });
            }
        }

        private static void WriteUpdateChanges<T>(T old, T @new, ObjectChanges changes)
            where T : class, IModel
        {
            foreach (var property in GetChangeTrackedProperties<T>())
            {
                var newValue = property.GetValue(@new);
                var oldValue = property.GetValue(old);
                if (newValue is null && oldValue is null)
                    continue;
                if (newValue is null || oldValue is null || !newValue.Equals(oldValue))
                    changes.Add(property.Name, new OldNewValue { Old = oldValue, New = newValue });
            }
        }

        private static void WriteAddChanges<T>(T @new, ObjectChanges changes) where T : class, IModel
        {
            foreach (var property in GetChangeTrackedProperties<T>())
            {
                var value = property.GetValue(@new);
                if (value is not null)
                    changes.Add(property.Name, new OldNewValue { New = value });
            }
        }

        public static ObjectChanges FromJson(string json)
        {
            var objectChanges = new ObjectChanges();
            var cs = json.Deserialize<Dictionary<string, OldNewValue>>();
            foreach (var (key, value) in cs)
            {
                objectChanges.Add(key,
                    new OldNewValue { Old = GetCleanValue(value.Old), New = GetCleanValue(value.New) });
            }

            return objectChanges;
        }

        private static object GetCleanValue(object o)
        {
            return o switch
            {
                null => null,
                NanoId id => id.Value,
                JsonElement element => element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.TryGetInt64(out var l) ? l :
                        element.TryGetDecimal(out var d) ? d : null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Array => element.EnumerateArray().Select(e => GetCleanValue(e)).ToArray(),
                    JsonValueKind.Object => ParseJsonObject(element.EnumerateObject()),
                    _ => null
                },
                _ => o
            };

            static object ParseJsonObject(JsonElement.ObjectEnumerator objectEnumerator)
            {
                var entries = objectEnumerator.ToList();
                return entries.Count == 1 &&
                       entries[0].Name.Equals("value", StringComparison.InvariantCultureIgnoreCase)
                    ? GetCleanValue(entries[0].Value)
                    : entries.ToDictionary(x => x.Name, x => GetCleanValue(x.Value));
            }
        }
    }
}
