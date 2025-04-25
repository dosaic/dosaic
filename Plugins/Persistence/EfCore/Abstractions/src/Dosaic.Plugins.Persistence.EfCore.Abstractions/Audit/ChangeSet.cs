using System.Collections;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public class ChangeSet<T> : List<ModelChange<T>> where T : class, IModel;

    internal class ChangeSet : List<ModelChange>
    {
        public Dictionary<Type, object> GetTypedChangeSets()
        {
            var typedChangeSets = new Dictionary<Type, object>();
            foreach (var change in this)
            {
                var type = (change.Entity?.GetType() ?? change.PreviousEntity?.GetType())!;
                if (!typedChangeSets.TryGetValue(type, out var value))
                {
                    var changeSetType = typeof(ChangeSet<>).MakeGenericType(type);
                    value = Activator.CreateInstance(changeSetType)!;
                    typedChangeSets[type] = value;
                }

                var changeSet = (IList)value;
                changeSet.Add(change.ToTyped(type));
            }
            return typedChangeSets;
        }
    }
}
