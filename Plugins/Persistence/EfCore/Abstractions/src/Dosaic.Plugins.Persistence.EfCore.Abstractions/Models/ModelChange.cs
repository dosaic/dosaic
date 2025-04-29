using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Models
{
    public class ModelChange<T>(ChangeState state, T entity, T previousEntity) where T : class, IModel
    {
        public ChangeState State { get; } = state;
        public T Entity { get; } = entity;
        public T PreviousEntity { get; } = previousEntity;
        public ObjectChanges GetChanges() => ObjectChanges.Calculate(State, PreviousEntity, Entity);
    }

    public class ModelChange(ChangeState state, IModel entity, IModel previousEntity)
    {
        public ChangeState State { get; } = state;
        public IModel Entity { get; } = entity;
        public IModel PreviousEntity { get; } = previousEntity;

        public object ToTyped(Type type)
        {
            return Activator.CreateInstance(typeof(ModelChange<>).MakeGenericType(type), State, Entity, PreviousEntity)!;
        }
        public static ModelChange Create(ChangeState state, object current, object old)
        {
            return new ModelChange(state, current as IModel, old as IModel);
        }
    }
}
