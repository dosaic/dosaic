using ArchUnitNET.Domain;
using AutoBogus;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using NSubstitute;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    public static class TestExtensions
    {
        private static readonly Type[] _types = AppDomain.CurrentDomain.GetAssemblies().GetTypes().ToArray();

        public static Type GetRealType(this Class cls)
        {
            return _types.FirstOrDefault(x => x.FullName == cls.FullName);
        }

        public static T Fake<T>(Action<T> configure = null) where T : class
        {
            var result = new AutoFaker<T>().Configure(c => c.WithRecursiveDepth(0)).Generate();
            configure?.Invoke(result);
            return result;
        }

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
