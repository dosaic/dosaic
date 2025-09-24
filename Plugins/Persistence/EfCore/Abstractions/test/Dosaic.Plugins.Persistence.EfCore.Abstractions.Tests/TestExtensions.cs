using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using NSubstitute;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    public static class TestExtensions
    {
        public static T GetModel<T>(Action<T> configure = null) where T : IModel
        {
            var m = Activator.CreateInstance<T>();
            m.Id = "123";
            configure?.Invoke(m);
            return m;
        }

        public static ITriggerContext<T> GetTriggerContext<T>(T entity, ChangeState changeState, T unmodified = null, IDb db = null) where T : class, IModel
        {
            var context = Substitute.For<ITriggerContext<T>>();
            var changeSet = new ChangeSet<T> { new ModelChange<T>(changeState, entity, unmodified) };
            context.ChangeSet.Returns(changeSet);
            context.Database.Returns(db);
            return context;
        }
    }
}
