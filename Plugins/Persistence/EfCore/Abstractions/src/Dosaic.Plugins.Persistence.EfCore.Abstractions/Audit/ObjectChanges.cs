using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
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

        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

        private static PropertyInfo[] GetChangeTrackedProperties<T>() =>
            _propertyCache.GetOrAdd(typeof(T), type => type.GetProperties()
                .Where(x => x.CanRead && x.Name != nameof(IModel.Id))
                .ToArray());

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
