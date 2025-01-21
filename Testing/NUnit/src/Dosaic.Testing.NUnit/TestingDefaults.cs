using Chronos;
using Microsoft.Extensions.DependencyInjection;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.DependencyInjection;

namespace Dosaic.Testing
{
    public static class TestingDefaults
    {
        public static ServiceCollection ServiceCollection()
        {
            var sc = new ServiceCollection();
            sc.AddDateTimeProvider().AddDateTimeOffsetProvider();
            sc.AddLogging();
            sc.AddTransient(typeof(IFactory<>), typeof(Factory<>));
            sc.AddSingleton<GlobalStatusCodeOptions>();
            return sc;
        }

        public static ServiceProvider ServiceProvider() => ServiceCollection().BuildServiceProvider();
    }
}
