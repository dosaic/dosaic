using Chronos;
using Dosaic.Hosting.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Testing.NUnit
{
    public static class TestingDefaults
    {
        public static ServiceCollection ServiceCollection()
        {
            var sc = new ServiceCollection();
            sc.AddDateTimeProvider().AddDateTimeOffsetProvider();
            sc.AddLogging();
            sc.AddSingleton<GlobalStatusCodeOptions>();
            return sc;
        }

        public static ServiceProvider ServiceProvider() => ServiceCollection().BuildServiceProvider();
    }
}
