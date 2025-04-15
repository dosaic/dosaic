using Chronos.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.InMemory.Tests
{
    public class InMemoryPluginTest
    {
        public InMemoryPluginTest()
        {
            ActivityTestBootstrapper.Setup();
        }

        [Test]
        public void CanRegisterGenericRepository()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton(Substitute.For<IDateTimeProvider>());
            sc.AddInMemoryRepository<SampleModel, Guid>();
            var plugin = new InMemoryPlugin();
            var expectedServices = new Dictionary<Type, Type>
            {
                {typeof(InMemoryStore), typeof(InMemoryStore)},
                {typeof(InMemoryRepository<SampleModel, Guid>), typeof(InMemoryRepository<SampleModel, Guid>)}
            };
            plugin.ShouldHaveServices(expectedServices, sc);
        }
    }
}
