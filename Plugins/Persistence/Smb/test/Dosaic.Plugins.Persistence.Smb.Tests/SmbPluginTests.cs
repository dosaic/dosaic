using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.Smb.Tests
{
    public class SmbPluginTests
    {
        [Test]
        public void PluginConfiguresServices()
        {
            var sc = TestingDefaults.ServiceCollection();
            sc.AddSingleton<SmbStorageConfiguration>();
            var plugin = new SmbStoragePlugin();
            plugin.ConfigureServices(sc);
            var expectedServices = new Dictionary<Type, Type>
            {
                {typeof(ISmbStorage), typeof(SmbStorage)}
            };
            plugin.ShouldHaveServices(expectedServices, sc);
        }
    }
}
